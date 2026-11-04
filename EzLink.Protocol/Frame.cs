using System;
using System.IO;

namespace EzLink.Protocol
{
    /// <summary>
    /// One protocol frame. Wire layout: Length (UInt32, bytes after this field) |
    /// command set (1) | command (1) | request id (UInt32) | payload.
    /// </summary>
    public sealed class Frame
    {
        /// <summary>Length field + command set + command + request id.</summary>
        public const int HeaderSize = 10;

        /// <summary>The part of the header that the Length field counts (set, command, request id).</summary>
        public const int CountedHeaderSize = 6;

        /// <summary>Largest payload that keeps Length within <see cref="ProtocolInfo.MaxFrameLength"/>.</summary>
        public const int MaxPayloadLength = ProtocolInfo.MaxFrameLength - CountedHeaderSize;

        private readonly byte commandSet;
        private readonly byte command;
        private readonly uint requestId;
        private readonly byte[] payload;

        public Frame(byte commandSet, byte command, uint requestId, byte[] payload)
        {
            if (payload == null)
            {
                payload = new byte[0];
            }
            if (payload.Length > MaxPayloadLength)
            {
                throw new ProtocolException("Payload of " + payload.Length + " bytes exceeds the frame limit");
            }
            this.commandSet = commandSet;
            this.command = command;
            this.requestId = requestId;
            this.payload = payload;
        }

        public byte CommandSet
        {
            get { return commandSet; }
        }

        public byte Command
        {
            get { return command; }
        }

        public uint RequestId
        {
            get { return requestId; }
        }

        public byte[] Payload
        {
            get { return payload; }
        }
    }

    /// <summary>Reads and writes frames on a stream (normally a NetworkStream).</summary>
    public static class FrameStream
    {
        /// <summary>Writes the frame with a single Write call so frames never interleave partially.</summary>
        public static void Write(Stream stream, Frame frame)
        {
            byte[] buffer = new byte[Frame.HeaderSize + frame.Payload.Length];
            uint length = (uint)(Frame.CountedHeaderSize + frame.Payload.Length);

            buffer[0] = (byte)length;
            buffer[1] = (byte)(length >> 8);
            buffer[2] = (byte)(length >> 16);
            buffer[3] = (byte)(length >> 24);
            buffer[4] = frame.CommandSet;
            buffer[5] = frame.Command;
            buffer[6] = (byte)frame.RequestId;
            buffer[7] = (byte)(frame.RequestId >> 8);
            buffer[8] = (byte)(frame.RequestId >> 16);
            buffer[9] = (byte)(frame.RequestId >> 24);
            Buffer.BlockCopy(frame.Payload, 0, buffer, Frame.HeaderSize, frame.Payload.Length);

            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
        }

        /// <summary>
        /// Reads one frame. Returns null if the stream ended cleanly before the first byte of a
        /// frame. Throws <see cref="ProtocolException"/> for a truncated frame, a length below the
        /// header size or above 1 MiB (checked before any payload allocation).
        /// </summary>
        public static Frame Read(Stream stream)
        {
            byte[] lengthBytes = new byte[4];
            int first = ReadFully(stream, lengthBytes, 0, 4);
            if (first == 0)
            {
                return null;
            }
            if (first < 4)
            {
                throw new ProtocolException("Truncated frame: stream ended inside the length field");
            }

            uint length = (uint)(lengthBytes[0] | (lengthBytes[1] << 8) | (lengthBytes[2] << 16) | (lengthBytes[3] << 24));
            if (length < Frame.CountedHeaderSize)
            {
                throw new ProtocolException("Frame length " + length + " is smaller than the header");
            }
            if (length > ProtocolInfo.MaxFrameLength)
            {
                throw new ProtocolException("Frame length " + length + " exceeds the 1 MiB limit");
            }

            byte[] body = new byte[length];
            if (ReadFully(stream, body, 0, body.Length) < body.Length)
            {
                throw new ProtocolException("Truncated frame: stream ended inside the frame body");
            }

            uint requestId = (uint)(body[2] | (body[3] << 8) | (body[4] << 16) | (body[5] << 24));
            byte[] payload = new byte[body.Length - Frame.CountedHeaderSize];
            Buffer.BlockCopy(body, Frame.CountedHeaderSize, payload, 0, payload.Length);
            return new Frame(body[0], body[1], requestId, payload);
        }

        /// <summary>Reads until <paramref name="count"/> bytes arrive or the stream ends; returns bytes read.</summary>
        private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }
    }
}
