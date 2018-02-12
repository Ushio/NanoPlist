using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NanoPlist {
    // https://github.com/msgpack/msgpack-cli
    [StructLayout(LayoutKind.Explicit)]
    public struct Float32Bits
    {
        [FieldOffset(0)]
        public readonly float Value;

        [FieldOffset(0)]
        public readonly Byte Byte0;

        [FieldOffset(1)]
        public readonly Byte Byte1;

        [FieldOffset(2)]
        public readonly Byte Byte2;

        [FieldOffset(3)]
        public readonly Byte Byte3;

        public Float32Bits(float value)
        {
            this = default(Float32Bits);
            this.Value = value;
        }

        /// <summary>
        /// Assign from big endian
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <param name="at">At.</param>
        public Float32Bits(byte[] bytes, ulong at)
        {
            this = default(Float32Bits);

            this.Byte0 = bytes[at + 3];
            this.Byte1 = bytes[at + 2];
            this.Byte2 = bytes[at + 1];
            this.Byte3 = bytes[at + 0];
        }

        /// <summary>
        /// Write as big endian
        /// </summary>
        /// <returns>The write.</returns>
        /// <param name="s">S.</param>
        public void Write(Stream s)
        {
            s.WriteByte(this.Byte3);
            s.WriteByte(this.Byte2);
            s.WriteByte(this.Byte1);
            s.WriteByte(this.Byte0);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Float64Bits
    {
        [FieldOffset(0)]
        public readonly double Value;

        [FieldOffset(0)]
        public readonly Byte Byte0;

        [FieldOffset(1)]
        public readonly Byte Byte1;

        [FieldOffset(2)]
        public readonly Byte Byte2;

        [FieldOffset(3)]
        public readonly Byte Byte3;

        [FieldOffset(4)]
        public readonly Byte Byte4;

        [FieldOffset(5)]
        public readonly Byte Byte5;

        [FieldOffset(6)]
        public readonly Byte Byte6;

        [FieldOffset(7)]
        public readonly Byte Byte7;

        public Float64Bits(double value)
        {
            this = default(Float64Bits);
            this.Value = value;
        }

        /// <summary>
        /// Assign from big endian
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <param name="at">At.</param>
        public Float64Bits(byte[] bytes, ulong at)
        {
            this = default(Float64Bits);

            this.Byte0 = bytes[at + 7];
            this.Byte1 = bytes[at + 6];
            this.Byte2 = bytes[at + 5];
            this.Byte3 = bytes[at + 4];
            this.Byte4 = bytes[at + 3];
            this.Byte5 = bytes[at + 2];
            this.Byte6 = bytes[at + 1];
            this.Byte7 = bytes[at + 0];
        }

        /// <summary>
        /// Write as big endian
        /// </summary>
        /// <returns>The write.</returns>
        /// <param name="s">S.</param>
        public void Write(Stream s)
        {
            s.WriteByte(this.Byte7);
            s.WriteByte(this.Byte6);
            s.WriteByte(this.Byte5);
            s.WriteByte(this.Byte4);
            s.WriteByte(this.Byte3);
            s.WriteByte(this.Byte2);
            s.WriteByte(this.Byte1);
            s.WriteByte(this.Byte0);
        }
    }
}

