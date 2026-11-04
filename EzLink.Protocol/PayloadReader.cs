using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EzLink.Protocol
{
    /// <summary>
    /// Reads a frame payload. Every read is bounds-checked against the bytes that are
    /// left, so truncated or hostile data raises <see cref="ProtocolException"/>
    /// instead of EndOfStreamException, huge allocations or garbage.
    /// </summary>
    public sealed class PayloadReader
    {
        private readonly byte[] data;
        private readonly MemoryStream stream;
        private readonly BinaryReader reader;

        public PayloadReader(byte[] payload)
        {
            data = payload == null ? new byte[0] : payload;
            stream = new MemoryStream(data, false);
            reader = new BinaryReader(stream);
        }

        public int Remaining
        {
            get { return data.Length - (int)stream.Position; }
        }

        private void Need(int count)
        {
            if (count < 0 || count > Remaining)
            {
                throw new ProtocolException("Truncated payload: need " + count + " bytes, " + Remaining + " left");
            }
        }

        /// <summary>Throws unless the whole payload has been consumed.</summary>
        public void EnsureEnd()
        {
            if (Remaining != 0)
            {
                throw new ProtocolException("Unexpected " + Remaining + " trailing bytes in payload");
            }
        }

        public byte ReadByte()
        {
            Need(1);
            return reader.ReadByte();
        }

        public bool ReadBool()
        {
            byte value = ReadByte();
            if (value > 1)
            {
                throw new ProtocolException("Invalid boolean value " + value);
            }
            return value == 1;
        }

        public ushort ReadUInt16()
        {
            Need(2);
            return reader.ReadUInt16();
        }

        public int ReadInt32()
        {
            Need(4);
            return reader.ReadInt32();
        }

        public uint ReadUInt32()
        {
            Need(4);
            return reader.ReadUInt32();
        }

        public long ReadInt64()
        {
            Need(8);
            return reader.ReadInt64();
        }

        /// <summary>Reads an Int64 that must not be negative (sizes, offsets).</summary>
        public long ReadNonNegativeInt64()
        {
            long value = ReadInt64();
            if (value < 0)
            {
                throw new ProtocolException("Negative value " + value + " where a size or offset was expected");
            }
            return value;
        }

        /// <summary>Reads exactly <paramref name="count"/> bytes with no length prefix.</summary>
        public byte[] ReadRaw(int count)
        {
            Need(count);
            return reader.ReadBytes(count);
        }

        /// <summary>Reads everything that is left.</summary>
        public byte[] ReadRemaining()
        {
            return ReadRaw(Remaining);
        }

        /// <summary>Reads a UInt16 length followed by that many bytes.</summary>
        public byte[] ReadBytes()
        {
            return ReadRaw(ReadUInt16());
        }

        public string ReadString()
        {
            byte[] bytes = ReadBytes();
            try
            {
                return PayloadWriter.Utf8.GetString(bytes);
            }
            catch (ArgumentException ex)
            {
                throw new ProtocolException("Invalid UTF-8 in string", ex);
            }
        }

        public Guid ReadGuid()
        {
            return new Guid(ReadRaw(16));
        }

        public DateTime ReadFileTime()
        {
            long value = ReadInt64();
            if (value < 0)
            {
                throw new ProtocolException("Negative FILETIME " + value);
            }
            try
            {
                return DateTime.FromFileTimeUtc(value);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new ProtocolException("FILETIME out of range: " + value, ex);
            }
        }

        /// <summary>Reads a 4-character ASCII tag and throws if it is not the expected one.</summary>
        public void ExpectMagic(string expected)
        {
            byte[] bytes = ReadRaw(4);
            if (Encoding.ASCII.GetString(bytes) != expected)
            {
                throw new ProtocolException("Bad magic, expected " + expected);
            }
        }

        /// <summary>
        /// Reads a UInt32 element count and verifies the remaining bytes could hold that many
        /// elements of at least <paramref name="minElementSize"/> bytes, so a forged count can
        /// never trigger a huge allocation.
        /// </summary>
        public int ReadCount(int minElementSize)
        {
            uint count = ReadUInt32();
            if ((long)count * minElementSize > Remaining)
            {
                throw new ProtocolException("Element count " + count + " exceeds payload size");
            }
            return (int)count;
        }

        public List<string> ReadStringList()
        {
            int count = ReadUInt16();
            if (count * 2 > Remaining)
            {
                throw new ProtocolException("String count " + count + " exceeds payload size");
            }
            List<string> list = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadString());
            }
            return list;
        }
    }
}
