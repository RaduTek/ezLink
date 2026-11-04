using System;
using System.IO;
using System.Text;

namespace EzLink.Protocol
{
    /// <summary>
    /// Builds a frame payload with the plan's encoding rules: little-endian integers,
    /// UInt16-length-prefixed UTF-8 strings, FILETIME timestamps, 16-byte GUIDs.
    /// </summary>
    public sealed class PayloadWriter
    {
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private static readonly DateTime FileTimeEpoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly MemoryStream stream = new MemoryStream();
        private readonly BinaryWriter writer;

        public PayloadWriter()
        {
            writer = new BinaryWriter(stream);
        }

        public int Length
        {
            get { return (int)stream.Length; }
        }

        public byte[] ToArray()
        {
            return stream.ToArray();
        }

        public void WriteByte(byte value)
        {
            writer.Write(value);
        }

        public void WriteBool(bool value)
        {
            writer.Write((byte)(value ? 1 : 0));
        }

        public void WriteUInt16(ushort value)
        {
            writer.Write(value);
        }

        public void WriteInt32(int value)
        {
            writer.Write(value);
        }

        public void WriteUInt32(uint value)
        {
            writer.Write(value);
        }

        public void WriteInt64(long value)
        {
            writer.Write(value);
        }

        /// <summary>Writes bytes with no length prefix (the rest of the payload).</summary>
        public void WriteRaw(byte[] value)
        {
            if (value != null)
            {
                writer.Write(value);
            }
        }

        /// <summary>Writes a UInt16 length followed by the bytes.</summary>
        public void WriteBytes(byte[] value)
        {
            int length = value == null ? 0 : value.Length;
            if (length > ushort.MaxValue)
            {
                throw new ProtocolException("Byte field too long: " + length);
            }
            writer.Write((ushort)length);
            WriteRaw(value);
        }

        public void WriteString(string value)
        {
            byte[] bytes = Utf8.GetBytes(value == null ? string.Empty : value);
            WriteBytes(bytes);
        }

        public void WriteGuid(Guid value)
        {
            writer.Write(value.ToByteArray());
        }

        /// <summary>Writes a UTC timestamp as an Int64 FILETIME. Dates before 1601 become 0.</summary>
        public void WriteFileTime(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local)
            {
                value = value.ToUniversalTime();
            }
            writer.Write(value < FileTimeEpoch ? 0L : value.ToFileTimeUtc());
        }

        /// <summary>Writes a fixed 4-character ASCII tag such as "EZLS".</summary>
        public void WriteMagic(string magic)
        {
            if (magic == null || magic.Length != 4)
            {
                throw new ArgumentException("Magic must be exactly 4 characters", "magic");
            }
            writer.Write(Encoding.ASCII.GetBytes(magic));
        }

        public void WriteStringList(System.Collections.Generic.IList<string> values)
        {
            int count = values == null ? 0 : values.Count;
            if (count > ushort.MaxValue)
            {
                throw new ProtocolException("List too long: " + count);
            }
            writer.Write((ushort)count);
            for (int i = 0; i < count; i++)
            {
                WriteString(values[i]);
            }
        }
    }
}
