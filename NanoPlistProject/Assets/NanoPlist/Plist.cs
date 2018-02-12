using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace NanoPlist
{
    using PlistList = System.Collections.Generic.List<object>;
    using PlistDictionary = System.Collections.Generic.Dictionary<string, object>;

    public class PlistException : System.Exception
    {
        public PlistException() { }
        public PlistException(string message) : base(message) { }
    }

    /*
        Binary Support
    */

    public class Plist
    {
        private static readonly Encoding UTF16BE = Encoding.GetEncoding("UTF-16BE");
        private static readonly Encoding ASCII = Encoding.GetEncoding("us-ascii", new EncoderExceptionFallback(), new DecoderExceptionFallback());
        private static readonly DateTime BaseTime = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private const byte TrueBit = 0x09;
        private const byte FalseBit = 0x08;

        private const byte TypeBitsBoolOrNil = 0x0;
        private const byte TypeBitsInteger = 0x10;
        private const byte TypeBitsReal = 0x20;
        private const byte TypeBitsDate = 0x30;
        private const byte TypeBitsBinaryData = 0x40;
        private const byte TypeBitsASCIIString = 0x50;
        private const byte TypeBitsUTF16String = 0x60;
        private const byte TypeBitsArray = 0xA0;
        private const byte TypeBitsDictionary = 0xD0;

        public static bool IsBinaryPlist(byte[] bytes)
        {
            if (bytes.Length < 8)
            {
                return false;
            }
            bool isMagicOK =
                bytes[0] == 'b' &&
                bytes[1] == 'p' &&
                bytes[2] == 'l' &&
                bytes[3] == 'i' &&
                bytes[4] == 's' &&
                bytes[5] == 't';
            bool isVerOK = bytes[6] == '0' && (bytes[7] == '0' || bytes[7] == '1');
            return isMagicOK && isVerOK;
        }
        public struct Trailer : IEquatable<Trailer>
        {
            public byte ShortVersion;
            public byte OffsetIntSize; /* 1, 2, 4, 8 */
            public byte ObjectRefSize; /* 1, 2, 4, 8 */
            public ulong NumObjects;
            public ulong TopObject;
            public ulong OffsetTableOffset;

            bool IEquatable<Trailer>.Equals(Trailer other)
            {
                return 
                    ShortVersion == other.ShortVersion && 
                    OffsetIntSize == other.OffsetIntSize && 
                    ObjectRefSize == other.ObjectRefSize && 
                    NumObjects == other.NumObjects &&
                    TopObject == other.TopObject &&
                    OffsetTableOffset == other.OffsetTableOffset;
            }
            public override int GetHashCode()
            {
                return 
                    ShortVersion.GetHashCode() ^
                    OffsetIntSize.GetHashCode() ^
                    ObjectRefSize.GetHashCode() ^
                    NumObjects.GetHashCode() ^
                    TopObject.GetHashCode() ^
                    OffsetTableOffset.GetHashCode();
            }
        }

        public static Trailer ReadTrailerBinary(byte[] bytes)
        {
            /*
                typedef struct {
                    uint8_t _unused[5];
                    uint8_t _sortVersion;
                    uint8_t _offsetIntSize;
                    uint8_t _objectRefSize;
                    uint64_t _numObjects;
                    uint64_t _topObject;
                    uint64_t _offsetTableOffset;
                } CFBinaryPlistTrailer;
            */
            var trailer = new Trailer();

            trailer.ShortVersion = bytes[bytes.Length - 27];
            trailer.OffsetIntSize = bytes[bytes.Length - 26];
            trailer.ObjectRefSize = bytes[bytes.Length - 25];
            trailer.NumObjects = BigEndianReader.ReadULong(bytes, (ulong)bytes.LongLength - 24);
            trailer.TopObject = BigEndianReader.ReadULong(bytes, (ulong)bytes.LongLength - 16);
            trailer.OffsetTableOffset = BigEndianReader.ReadULong(bytes, (ulong)bytes.LongLength - 8);

            return trailer;
        }

        private static ulong ReadOffset(byte[] bytes, Trailer trailer, ulong objectRef)
        {
            ulong at = trailer.OffsetTableOffset + objectRef * trailer.OffsetIntSize;
            return BigEndianReader.ReadNBytesUnsignedInteger(bytes, trailer.OffsetIntSize, at);
        }

        private static int TwoPowNNNN(int nnnn)
        {
            return nnnn == 0 ? 1 : 2 << (nnnn - 1);
        }

        private static long ReadInteger(byte[] bytes, ulong at, out ulong nextAt)
        {
            byte headbyte = bytes[at];
            int nnnn = headbyte & 0x0F;
            int nnnn_bytes = TwoPowNNNN(nnnn);
            long integer = BigEndianReader.ReadNBytesInteger(bytes, nnnn_bytes, at + 1);
            nextAt = at + 1 + (ulong)nnnn_bytes;
            return integer;
        }
        private static void IntNNNNorNextInt(byte[] bytes, ulong objectAt, int nnnn, out ulong intValue, out ulong nextAt)
        {
            if (nnnn < 15)
            {
                intValue = (ulong)nnnn;
                nextAt = objectAt + 1;
            }
            else
            {
                intValue = (ulong)ReadInteger(bytes, objectAt + 1, out nextAt);
            }
        }

        public static object ReadObjectBinary(byte[] bytes)
        {
            if(IsBinaryPlist(bytes) == false) {
                throw new PlistException("is not binary plist");
            }
            Trailer trailer = ReadTrailerBinary(bytes);
            return ReadObjectBinary(bytes, ref trailer, trailer.TopObject);
        }
        private static object ReadObjectBinary(byte[] bytes, ref Trailer trailer, ulong objectRef)
        {
            ulong objectAt = ReadOffset(bytes, trailer, objectRef);
            byte headbyte = bytes[objectAt];

            int type = headbyte & 0xF0;
            int nnnn = headbyte & 0x0F;

            switch (type)
            {
                case TypeBitsBoolOrNil:  /* 0000 null, false, true*/
                    switch (nnnn)
                    {
                        case 0x0:
                            throw new PlistException("unsupported null");
                        case FalseBit:
                            return false;
                        case TrueBit:
                            return true;
                        default:
                            throw new PlistException("undefined value");
                    }
                case TypeBitsInteger:  /* 0001 integer */
                    {
                        ulong nextAt;
                        long integer = ReadInteger(bytes, objectAt, out nextAt);
                        if (integer < int.MinValue || int.MaxValue < integer)
                        {
                            throw new PlistException("int overflow, we use int for simplity.");
                        }
                        return (int)integer;
                    }
                case TypeBitsReal:  /* 0010 real */
                    {
                        int nnnn_bytes = TwoPowNNNN(nnnn);
                        var real = BigEndianReader.ReadNBytesReal(bytes, nnnn_bytes, objectAt + 1);
                        return real;
                    }
                case TypeBitsDate:  /* 0011 date */
                    {
                        double elapsed = BigEndianReader.ReadNBytesReal(bytes, 8, objectAt + 1);
                        var span = TimeSpan.FromSeconds(elapsed);
                        DateTime date = BaseTime.Add(span).ToLocalTime();
                        return date;
                    }
                case TypeBitsBinaryData:  /* 0100 data */
                    {
                        ulong dataLength;
                        ulong dataAt;
                        IntNNNNorNextInt(bytes, objectAt, nnnn, out dataLength, out dataAt);

                        byte[] data = new byte[dataLength];
                        Array.Copy(bytes, (long)dataAt, data, 0, (long)dataLength);
                        return data;
                    }
                case TypeBitsASCIIString:  /* 0101 ascii string */
                    {
                        ulong asciiCount;
                        ulong asciiAt;
                        IntNNNNorNextInt(bytes, objectAt, nnnn, out asciiCount, out asciiAt);
                        string ascii;
                        if (asciiAt + asciiCount < int.MaxValue)
                        {
                            ascii = System.Text.Encoding.ASCII.GetString(bytes, (int)asciiAt, (int)asciiCount);
                        }
                        else
                        {
                            byte[] asciiBytes = new byte[asciiCount];
                            Array.Copy(bytes, (long)asciiAt, asciiBytes, 0, (long)asciiCount);
                            ascii = System.Text.Encoding.ASCII.GetString(asciiBytes, 0, (int)asciiBytes.Length);
                        }
                        return ascii;
                    }
                case TypeBitsUTF16String:  /* 0110 utf16 string */
                    {
                        ulong utf16Count;
                        ulong utf16At;
                        IntNNNNorNextInt(bytes, objectAt, nnnn, out utf16Count, out utf16At);
                        ulong utf16byteLength = utf16Count * 2;

                        string utf16;
                        if (utf16At + utf16byteLength < int.MaxValue)
                        {
                            utf16 = UTF16BE.GetString(bytes, (int)utf16At, (int)utf16byteLength);
                        }
                        else
                        {
                            byte[] utf16Bytes = new byte[utf16byteLength];
                            Array.Copy(bytes, (long)utf16At, utf16Bytes, 0, (long)utf16byteLength);
                            utf16 = UTF16BE.GetString(utf16Bytes, 0, utf16Bytes.Length);
                        }
                        return utf16;
                    }
                case TypeBitsArray:  /* 1010 array */
                    {
                        ulong count;
                        ulong arrayAt;
                        IntNNNNorNextInt(bytes, objectAt, nnnn, out count, out arrayAt);

                        int capacity = (int)(Math.Min(count, int.MaxValue));
                        PlistList objectArray = new PlistList(capacity);
                        for (ulong i = 0; i < count; ++i)
                        {
                            ulong arrayObjectRef = BigEndianReader.ReadNBytesUnsignedInteger(bytes, trailer.ObjectRefSize, arrayAt + trailer.ObjectRefSize * i);
                            objectArray.Add(ReadObjectBinary(bytes, ref trailer, arrayObjectRef));
                        }
                        return objectArray;
                    }
                case TypeBitsDictionary: /* 1101 dictionary */
                    {
                        ulong count;
                        ulong dictionaryAt;
                        IntNNNNorNextInt(bytes, objectAt, nnnn, out count, out dictionaryAt);
                        int capacity = (int)(Math.Min(count, int.MaxValue));
                        PlistDictionary objectDictionary = new PlistDictionary(capacity);

                        /* key, key, key, value, value, value... */
                        ulong keyBytes = trailer.ObjectRefSize * count;
                        for (ulong i = 0; i < count; ++i)
                        {
                            ulong keyObjectRef = BigEndianReader.ReadNBytesUnsignedInteger(bytes, trailer.ObjectRefSize, dictionaryAt + trailer.ObjectRefSize * i);
                            ulong valueObjectRef = BigEndianReader.ReadNBytesUnsignedInteger(bytes, trailer.ObjectRefSize, dictionaryAt + keyBytes + trailer.ObjectRefSize * i);
                            string keyObject = ReadObjectBinary(bytes, ref trailer, keyObjectRef) as string;
                            object valueObject = ReadObjectBinary(bytes, ref trailer, valueObjectRef);
                            objectDictionary[keyObject] = valueObject;
                        }
                        return objectDictionary;
                    }
                default:
                    throw new PlistException("undefined data type");
            }
        }

        public struct ObjectAnalytics
        {
            public static readonly ObjectAnalytics Nil = new ObjectAnalytics() { MaxStringEncodingBytes = 0, NumberOfObject = 0 };

            public ulong NumberOfObject;
            public ulong MaxStringEncodingBytes;

            public ObjectAnalytics Merge(ObjectAnalytics rhs)
            {
                return new ObjectAnalytics()
                {
                    MaxStringEncodingBytes = Math.Max(this.MaxStringEncodingBytes, rhs.MaxStringEncodingBytes),
                    NumberOfObject = this.NumberOfObject + rhs.NumberOfObject
                };
            }
        }

        public static ObjectAnalytics AnalyzeObject(object root)
        {
            if (root == null)
            {
                throw new PlistException("unsupported null");
            }

            if (
                root is bool ||
                root is int ||
                root is double ||
                root is DateTime ||
                root is byte[]
            )
            {
                return new ObjectAnalytics() { MaxStringEncodingBytes = 0, NumberOfObject = 1 };
            }

            if (root is string)
            {
                return new ObjectAnalytics() { MaxStringEncodingBytes = (ulong)UTF16BE.GetMaxByteCount(((string)root).Length), NumberOfObject = 1 };
            }
            if (root is PlistList)
            {
                ObjectAnalytics oa = ObjectAnalytics.Nil;
                foreach (var o in root as PlistList)
                {
                    oa = oa.Merge(AnalyzeObject(o));
                }
                oa.NumberOfObject += 1; /* me */
                return oa;
            }
            if (root is PlistDictionary)
            {
                ObjectAnalytics oa = ObjectAnalytics.Nil;
                foreach (var o in root as PlistDictionary)
                {
                    oa = oa.Merge(
                        AnalyzeObject(o.Key).Merge(AnalyzeObject(o.Value))
                    );
                }

                oa.NumberOfObject += 1; /* me */
                return oa;
            }
            throw new PlistException("undefined data type");
        }

        private static byte MinBytesUnsignedInteger(ulong integer)
        {
            // 
            if (integer <= byte.MaxValue)
            {
                return 1;
            }
            if (integer <= ushort.MaxValue)
            {
                return 2;
            }
            if (integer <= int.MaxValue)
            {
                return 4;
            }
            return 8;
        }
        private static byte MinBytesInteger(long integer)
        {
            if (integer < 0)
            {
                return 8;
            }
            if (integer <= byte.MaxValue)
            {
                return 1;
            }
            if (integer <= ushort.MaxValue)
            {
                return 2;
            }
            if (integer <= uint.MaxValue)
            {
                return 4;
            }
            return 8;
        }

        private static void WriteInteger(long integer, Stream stream)
        {
            int minBytes = MinBytesInteger(integer);
            switch (minBytes)
            {
                case 1:
                    {
                        // unsigned
                        byte headbyte = TypeBitsInteger | 0x0;
                        stream.WriteByte(headbyte);
                        stream.WriteByte((byte)integer);
                        break;
                    }
                case 2:
                    {
                        // unsigned
                        byte headbyte = TypeBitsInteger | 0x1;
                        stream.WriteByte(headbyte);
                        BigEndianWriter.WriteUShort(stream, (ushort)integer);
                        break;
                    }
                case 4:
                    {
                        // unsigned
                        byte headbyte = TypeBitsInteger | 0x2;
                        stream.WriteByte(headbyte);
                        BigEndianWriter.WriteUInt(stream, (uint)integer);
                        break;
                    }
                case 8:
                    {
                        // signed
                        byte headbyte = TypeBitsInteger | 0x3;
                        stream.WriteByte(headbyte);
                        BigEndianWriter.WriteLong(stream, (long)integer);
                        break;
                    }
                default:
                    throw new PlistException();
            }
        }
        public static void WriteTrailerBinary(Stream stream, Trailer trailer)
        {
            for (int i = 0; i < 5; ++i)
            {
                stream.WriteByte(0);
            }
            stream.WriteByte(trailer.ShortVersion);
            stream.WriteByte(trailer.OffsetIntSize);
            stream.WriteByte(trailer.ObjectRefSize);

            BigEndianWriter.WriteULong(stream, trailer.NumObjects);
            BigEndianWriter.WriteULong(stream, trailer.TopObject);
            BigEndianWriter.WriteULong(stream, trailer.OffsetTableOffset);
        }
        private static ulong WriteObjectBinary(Stream stream, object root, byte objectRefSize, byte[] encodeBuffer, ref ulong objectIndex, List<ulong> offsetTable)
        {
            ulong objectRef = objectIndex++;
            ulong objectAt = (ulong)stream.Position;
            offsetTable.Add(objectAt);

            if (root == null)
            {
                throw new PlistException("unsupported null");
            }
            else if (root is bool)
            {
                if ((bool)root)
                {
                    stream.WriteByte(TypeBitsBoolOrNil | TrueBit);
                }
                else
                {
                    stream.WriteByte(TypeBitsBoolOrNil | FalseBit);
                }
            }
            else if (root is int)
            {
                WriteInteger((int)root, stream);
            }
            else if (root is double)
            {
                // real, 2^3 = 8 byte
                // 0010 0011
                // force use double
                byte headbyte = TypeBitsReal | 0x03;
                stream.WriteByte(headbyte);
                var f64 = new Float64Bits((double)root);
                f64.Write(stream);
            }
            else if (root is DateTime)
            {
                DateTime date = (DateTime)root;
                TimeSpan elapsedSpan = date.ToUniversalTime() - BaseTime;
                double elapsed = elapsedSpan.TotalSeconds;

                byte headbyte = TypeBitsDate | 0x03;
                stream.WriteByte(headbyte);
                var f64 = new Float64Bits(elapsed);
                f64.Write(stream);
            }
            else if (root is string)
            {
                string text = root as string;

                byte typebit;
                int count;
                int writeBytes;
                try
                {
                    typebit = TypeBitsASCIIString;
                    writeBytes = ASCII.GetBytes(text, 0, text.Length, encodeBuffer, 0);
                    count = writeBytes;
                }
                catch (EncoderFallbackException)
                {
                    typebit = TypeBitsUTF16String;
                    writeBytes = UTF16BE.GetBytes(text, 0, text.Length, encodeBuffer, 0);
                    count = writeBytes >> 1;
                }

                if (count < 15)
                {
                    byte headbyte = (byte)(typebit | count);
                    stream.WriteByte(headbyte);
                }
                else
                {
                    byte headbyte = (byte)(typebit | 0x0F);
                    stream.WriteByte(headbyte);
                    WriteInteger((long)count, stream);
                }
                stream.Write(encodeBuffer, 0, writeBytes);
            }
            else if (root is byte[])
            {
                byte[] data = root as byte[];
                byte typebit = TypeBitsBinaryData;
                if (data.Length < 15)
                {
                    byte headbyte = (byte)(typebit | data.Length);
                    stream.WriteByte(headbyte);
                }
                else
                {
                    byte headbyte = (byte)(typebit | 0x0F);
                    stream.WriteByte(headbyte);
                    WriteInteger(data.LongLength, stream);
                }
                stream.Write(data, 0, data.Length);
            }
            else if (root is PlistList)
            {
                PlistList objectArray = root as PlistList;
                byte typebit = TypeBitsArray;
                if (objectArray.Count < 15)
                {
                    byte headbyte = (byte)(typebit | objectArray.Count);
                    stream.WriteByte(headbyte);
                }
                else
                {
                    byte headbyte = (byte)(typebit | 0x0F);
                    stream.WriteByte(headbyte);
                    WriteInteger(objectArray.Count, stream);
                }

                ulong arrayAt = (ulong)stream.Position;

                // reserve data
                ulong reserveBytes = (ulong)objectArray.Count * objectRefSize;
                for (ulong i = 0; i < reserveBytes; ++i)
                {
                    stream.WriteByte(0);
                }
                for (int i = 0; i < objectArray.Count; ++i)
                {
                    ulong arrayObjectRef = WriteObjectBinary(stream, objectArray[i], objectRefSize, encodeBuffer, ref objectIndex, offsetTable);
                    stream.Seek((long)arrayAt + i * objectRefSize, SeekOrigin.Begin);
                    BigEndianWriter.WriteNBytesUnsignedInteger(stream, arrayObjectRef, objectRefSize);
                    stream.Seek(0, SeekOrigin.End);
                }
            }
            else if (root is PlistDictionary)
            {
                PlistDictionary objectDictionary = root as PlistDictionary;
                byte typebit = TypeBitsDictionary;
                if (objectDictionary.Count < 15)
                {
                    byte headbyte = (byte)(typebit | objectDictionary.Count);
                    stream.WriteByte(headbyte);
                }
                else
                {
                    byte headbyte = (byte)(typebit | 0x0F);
                    stream.WriteByte(headbyte);
                    WriteInteger(objectDictionary.Count, stream);
                }

                ulong dictionaryAt = (ulong)stream.Position;

                // reserve data
                ulong reserveBytes = (ulong)objectDictionary.Count * 2 * objectRefSize;
                for (ulong j = 0; j < reserveBytes; ++j)
                {
                    stream.WriteByte(0);
                }

                /* key, key, key, value, value, value... */
                long keyBytes = objectRefSize * (long)objectDictionary.Count;
                int i = 0;
                foreach (var objectKeyValue in objectDictionary)
                {
                    ulong arrayKeyRef = WriteObjectBinary(stream, objectKeyValue.Key, objectRefSize, encodeBuffer, ref objectIndex, offsetTable);
                    ulong arrayValueRef = WriteObjectBinary(stream, objectKeyValue.Value, objectRefSize, encodeBuffer, ref objectIndex, offsetTable);

                    stream.Seek((long)dictionaryAt + i * objectRefSize, SeekOrigin.Begin);
                    BigEndianWriter.WriteNBytesUnsignedInteger(stream, arrayKeyRef, objectRefSize);

                    stream.Seek((long)dictionaryAt + keyBytes + i * objectRefSize, SeekOrigin.Begin);
                    BigEndianWriter.WriteNBytesUnsignedInteger(stream, arrayValueRef, objectRefSize);
                    stream.Seek(0, SeekOrigin.End);

                    i++;
                }
            }
            else
            {
                throw new PlistException();
            }
            return objectRef;
        }

        private static byte MinOffsetIntSize(long bodySize, int offsetTableCount) {
            // 1 byte
            long oneByteMinAddress = bodySize + offsetTableCount * 1 - 1; 
            if(oneByteMinAddress < byte.MaxValue) {
                return 1;
            }

            // 2 byte
            long twoByteMinAddress = bodySize + offsetTableCount * 2 - 1;
            if (twoByteMinAddress < ushort.MaxValue)
            {
                return 2;
            }

            // 4 byte
            long fourByteMinAddress = bodySize + offsetTableCount * 4 - 1;
            if (fourByteMinAddress < uint.MaxValue)
            {
                return 4;
            }
            return 8;
        }

        public static byte[] WriteObjectBinary(object root)
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)'b');
            stream.WriteByte((byte)'p');
            stream.WriteByte((byte)'l');
            stream.WriteByte((byte)'i');
            stream.WriteByte((byte)'s');
            stream.WriteByte((byte)'t');
            stream.WriteByte((byte)'0');
            stream.WriteByte((byte)'0');

            var analytics = AnalyzeObject(root);
            byte objectRefSize = MinBytesUnsignedInteger(analytics.NumberOfObject);
            byte[] encodeBuffer = new byte[analytics.MaxStringEncodingBytes];

            ulong objectIndex = 0;
            List<ulong> offsetTable = new List<ulong>((int)analytics.NumberOfObject /* capacity */);

            WriteObjectBinary(stream, root, objectRefSize, encodeBuffer, ref objectIndex, offsetTable);

            byte offsetIntSize = MinOffsetIntSize(stream.Position, offsetTable.Count);

            Trailer trailer = new Trailer()
            {
                ShortVersion = 0,
                OffsetIntSize = offsetIntSize,
                ObjectRefSize = objectRefSize,
                NumObjects = analytics.NumberOfObject,
                TopObject = 0,
                OffsetTableOffset = (ulong)stream.Position,
            };

            foreach (ulong offset in offsetTable)
            {
                BigEndianWriter.WriteNBytesUnsignedInteger(stream, offset, offsetIntSize);
            }

            WriteTrailerBinary(stream, trailer);

            return stream.ToArray();
        }

        public static bool EqualObject(object x, object y, double deltaReal, double deltaDateSeconds) {
            if(x.GetType() != y.GetType()) {
                return false;
            }

            if (x is bool || x is int || x is string) {
                bool eq = x.Equals(y);
                return eq;
            }

            if(x is byte[]) {
                bool eq = (x as byte[]).SequenceEqual(y as byte[]);
                return eq;
            }
            if(x is double) {
                bool eq = Math.Abs((double)x - (double)y) < deltaReal;
                return eq;
            }
            if(x is DateTime) {
                var deltaTime = (DateTime)x - (DateTime)y;
                bool eq = Math.Abs(deltaTime.TotalSeconds) < deltaDateSeconds;
                return eq;
            }

            if (x is PlistList)
            {
                PlistList xList = x as PlistList;
                PlistList yList = y as PlistList;
                if(xList.Count != yList.Count) {
                    return false;
                }
                for(int i = 0 ; i < xList.Count ; ++i) {
                    if(EqualObject(xList[i], yList[i], deltaReal, deltaDateSeconds) == false) {
                        return false;
                    }
                }
                return true;
            }
            if (x is PlistDictionary)
            {
                PlistDictionary xDict = x as PlistDictionary;
                PlistDictionary yDict = y as PlistDictionary;
                if(xDict.Count != yDict.Count) {
                    return false;
                }

                foreach(var kv in xDict) {
                    if(yDict.ContainsKey(kv.Key) == false) {
                        return false;
                    }
                    var yValue = yDict[kv.Key];
                    if(EqualObject(kv.Value , yValue, deltaReal, deltaDateSeconds) == false) {
                        return false;
                    }
                }
                return true;
            }
            throw new PlistException("undefined data type");
        }

        /*
            XML Support
        */
        private static readonly string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        private static T[] ArrayRemoveRange<T>(T[] array, long startIndex, long length)
        {
            if (startIndex < 0) {
                throw new ArgumentOutOfRangeException("startIndex");
            }
            if (startIndex + length > array.LongLength) {
                throw new ArgumentOutOfRangeException("length");
            }

            T[] newArray = new T[array.LongLength - length];

            Array.Copy(array, 0, newArray, 0, startIndex);
            Array.Copy(array, startIndex + length, newArray, startIndex, array.Length - startIndex - length);

            return newArray;
        }

        // <!DOCTYPE plist
        private static readonly byte[] DTDHead = new byte[] {
            0x3C, 0x21, 0x44, 0x4F, 0x43, 0x54, 0x59, 0x50, 0x45, 0x20, 0x70, 0x6C, 0x69, 0x73, 0x74
        };
        private static byte[] RemoveDTD(byte[] xmlbytes) {
            for (long i = 0; i < xmlbytes.LongLength; ++i) {
                bool match = true;
                for (int j = 0; j < DTDHead.Length ; ++j)
                {
                    if (xmlbytes[i + j] != DTDHead[j])
                    {
                        match = false;
                        break;
                    }
                }
                if(match) {
                    for (long j = i + DTDHead.Length; j < xmlbytes.LongLength; ++j) {
                        if(xmlbytes[j] == 0x3E /* > */) {
                            return ArrayRemoveRange(xmlbytes, i, j - i + 1);
                        }
                    }
                }
            }
            return xmlbytes;
        }

        private static object ReadObjectXML(XElement element) {
            switch(element.Name.LocalName) {
            case "false": {
                return false;
            }
            case "true": {
                return true;
            }
            case "integer": {
                return int.Parse(element.Value);
            }
            case "real": {
                return double.Parse(element.Value);
            }
            case "string": {
                return element.Value;
            }
            case "date": {
                var utc = DateTime.ParseExact(element.Value, DateFormat, System.Globalization.CultureInfo.InvariantCulture);
                return utc.ToLocalTime();
            }
            case "data": {
                return System.Convert.FromBase64String(element.Value);
            }
            case "dict": {
                Dictionary<string, object> dictObject = new Dictionary<string, object>();
                string key = "";
                foreach(var kvElement in element.Elements()) {
                    if(kvElement.Name.LocalName == "key") {
                        key = kvElement.Value;
                    } else {
                        dictObject[key] = ReadObjectXML(kvElement);
                        key = "";
                    }
                }
                return dictObject;
            }
            case "array": {
                List<object> arrayObject = new List<object>();
                foreach(var arrayElement in element.Elements()) {
                    arrayObject.Add(ReadObjectXML(arrayElement));
                }
                return arrayObject;
            }
            default:
                throw new PlistException("undefined data type");
            }
        }

        public static object ReadObjectXML(byte[] bytes) {
            byte[] xmldata = RemoveDTD(bytes);
            XDocument document = XDocument.Load(new MemoryStream(xmldata));
            XElement plistRoot = document.Root.Elements().First();
            return ReadObjectXML(plistRoot);
        }

        private static XElement BuildElementXML(object root) {
            if (root == null)
            {
                throw new PlistException("unsupported null");
            }
            else if (root is bool)
            {
                if ((bool)root)
                {
                    return new XElement("true");
                }
                else
                {
                    return new XElement("false");
                }
            }
            else if (root is int)
            {
                return new XElement("integer", root.ToString());
            }
            else if (root is double)
            {
                return new XElement("real", root.ToString());
            }
            else if (root is DateTime)
            {
                DateTime date = (DateTime)root;
                var str = date.ToUniversalTime().ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);
                return new XElement("date", str);
            }
            else if (root is string)
            {
                string text = root as string;
                return new XElement("string", text);
            }
            else if (root is byte[])
            {
                byte[] data = root as byte[];
                var str = System.Convert.ToBase64String(data);
                return new XElement("data", str);
            }
            else if (root is PlistList)
            {
                PlistList objectArray = root as PlistList;
                object[] contents = new object[objectArray.Count];
                for(int i = 0; i < objectArray.Count ; ++i) {
                    contents[i] = BuildElementXML(objectArray[i]);
                }
                return new XElement("array", contents);
            }
            else if (root is PlistDictionary)
            {
                PlistDictionary objectDictionary = root as PlistDictionary;
                object[] contents = new object[objectDictionary.Count * 2];
                int i = 0;
                foreach(var kv in objectDictionary) {
                    contents[i++] = new XElement("key", kv.Key);
                    contents[i++] = BuildElementXML(kv.Value);
                }
                return new XElement("dict", contents);
            }
            else
            {
                throw new PlistException("undefined data type");
            }
        }
        public static byte[] WriteObjectXML(object root, bool minimal = false) {
            XDocument document = new XDocument(
                new XDeclaration("1.0", "utf8", null),
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist", new XAttribute("version", "1.0"), BuildElementXML(root))
            );
            MemoryStream stream = new MemoryStream();
            TextWriter textWriter = new StreamWriter(stream);
            document.Save(textWriter, minimal ? SaveOptions.DisableFormatting : SaveOptions.None);
            return stream.ToArray();
        }
    }
}