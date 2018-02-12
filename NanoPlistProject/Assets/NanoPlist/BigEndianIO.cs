using System.IO;

namespace NanoPlist {
    public static class BigEndianReader
    {
        public static ushort ReadUShort(byte[] bytes, ulong at)
        {
            return (ushort)((ushort)bytes[at + 0] << 8 | (ushort)bytes[at + 1]);
        }
        public static short ReadShort(byte[] bytes, ulong at)
        {
            return (short)ReadUShort(bytes, at);
        }
        public static uint ReadUInt(byte[] bytes, ulong at)
        {
            return
                (uint)bytes[at + 0] << 24 |
                (uint)bytes[at + 1] << 16 |
                (uint)bytes[at + 2] << 8 |
                (uint)bytes[at + 3];
        }

        public static ulong ReadULong(byte[] bytes, ulong at)
        {
            return
                (ulong)bytes[at + 0] << 56 |
                (ulong)bytes[at + 1] << 48 |
                (ulong)bytes[at + 2] << 40 |
                (ulong)bytes[at + 3] << 32 |
                (ulong)bytes[at + 4] << 24 |
                (ulong)bytes[at + 5] << 16 |
                (ulong)bytes[at + 6] << 8 |
                (ulong)bytes[at + 7];
        }

        // n = {1, 2, 4, 8}
        public static ulong ReadNBytesUnsignedInteger(byte[] bytes, int n, ulong at)
        {
            switch (n)
            {
                case 1:
                    return bytes[at];
                case 2:
                    return ReadUShort(bytes, at);
                case 4:
                    return ReadUInt(bytes, at);
                case 8:
                    return ReadULong(bytes, at);
                default:
                    throw new PlistException("undefined byte size");
            }
        }
        // n = {1, 2, 4, 8}
        // 1, 2, 4 is unsigned,
        // 8 is signed
        // https://opensource.apple.com/source/CF/CF-550/CFBinaryPList.c
        public static long ReadNBytesInteger(byte[] bytes, int n, ulong at)
        {
            switch (n)
            {
                case 1:
                    // unsigned
                    return (long)bytes[at];
                case 2:
                    // unsigned
                    return (long)ReadUShort(bytes, at);
                case 4:
                    // unsigned
                    return (long)ReadUInt(bytes, at);
                case 8:
                    // signed
                    return (long)ReadULong(bytes, at);
                default:
                    throw new PlistException("undefined byte size");
            }
        }

        // n = {4, 8}
        public static double ReadNBytesReal(byte[] bytes, int n, ulong at)
        {
            switch (n)
            {
                case 4:
                    var f32 = new Float32Bits(bytes, at);
                    return f32.Value;
                case 8:
                    var f64 = new Float64Bits(bytes, at);
                    return f64.Value;
                default:
                    throw new PlistException("undefined byte size");
            }
        }
    }

    public class BigEndianWriter
    {
        public static void WriteUShort(Stream s, ushort value)
        {
            s.WriteByte((byte)((value >> 8) & 0xff));
            s.WriteByte((byte)(value & 0xff));
        }
        public static void WriteUInt(Stream s, uint value)
        {
            s.WriteByte((byte)((value >> 24) & 0xff));
            s.WriteByte((byte)((value >> 16) & 0xff));
            s.WriteByte((byte)((value >> 8) & 0xff));
            s.WriteByte((byte)(value & 0xff));
        }
        public static void WriteLong(Stream s, long value)
        {
            WriteULong(s, (ulong)value);
        }
        public static void WriteULong(Stream s, ulong value)
        {
            s.WriteByte((byte)((value >> 56) & 0xff));
            s.WriteByte((byte)((value >> 48) & 0xff));
            s.WriteByte((byte)((value >> 40) & 0xff));
            s.WriteByte((byte)((value >> 32) & 0xff));
            s.WriteByte((byte)((value >> 24) & 0xff));
            s.WriteByte((byte)((value >> 16) & 0xff));
            s.WriteByte((byte)((value >> 8) & 0xff));
            s.WriteByte((byte)(value & 0xff));
        }

        public static void WriteNBytesUnsignedInteger(Stream s, ulong integer, int n)
        {
            switch (n)
            {
                case 1:
                    s.WriteByte((byte)integer);
                    break;
                case 2:
                    WriteUShort(s, (ushort)integer);
                    break;
                case 4:
                    WriteUInt(s, (uint)integer);
                    break;
                case 8:
                    WriteULong(s, integer);
                    break;
                default:
                    throw new PlistException("undefined byte size");
            }
        }
    }
}