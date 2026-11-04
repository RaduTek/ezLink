using System;
using System.IO;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class FrameTests
    {
        /// <summary>A stream that hands out at most one byte per Read, like a slow socket.</summary>
        private sealed class TrickleStream : MemoryStream
        {
            public TrickleStream(byte[] data)
                : base(data)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return base.Read(buffer, offset, count > 0 ? 1 : 0);
            }
        }

        private static byte[] Encode(Frame frame)
        {
            MemoryStream ms = new MemoryStream();
            FrameStream.Write(ms, frame);
            return ms.ToArray();
        }

        private static Frame Decode(byte[] bytes)
        {
            return FrameStream.Read(new MemoryStream(bytes));
        }

        [Test]
        public void HeaderLayoutMatchesThePlan()
        {
            Frame frame = new Frame(0x01, 0x0A, 0x04030201, new byte[] { 0xAA, 0xBB });
            Assert.AreEqual(
                new byte[]
                {
                    8, 0, 0, 0,        // length = 6 + 2 payload bytes, little-endian
                    0x01, 0x0A,        // command set, command
                    1, 2, 3, 4,        // request id, little-endian
                    0xAA, 0xBB
                },
                Encode(frame));
        }

        [Test]
        public void FrameRoundTripKeepsEveryField()
        {
            Frame frame = new Frame(0x01, 0x0B, 0xFFFFFFFE, Samples.Bytes(1000));
            Frame back = Decode(Encode(frame));
            Assert.AreEqual(frame.CommandSet, back.CommandSet);
            Assert.AreEqual(frame.Command, back.Command);
            Assert.AreEqual(frame.RequestId, back.RequestId);
            Assert.AreEqual(frame.Payload, back.Payload);
        }

        [Test]
        public void EmptyPayloadRoundTrips()
        {
            Frame back = Decode(Encode(new Frame(0, 3, 0, null)));
            Assert.AreEqual(0, back.Payload.Length);
            Assert.AreEqual(0u, back.RequestId);
        }

        [Test]
        public void BigEndianThresholdsDecodeCorrectly()
        {
            // Lengths and request ids with high bits set in every byte position.
            Frame frame = new Frame(0xFF, 0xFF, 0x80FF00FF, new byte[0x1FF00]);
            Frame back = Decode(Encode(frame));
            Assert.AreEqual(0x80FF00FFu, back.RequestId);
            Assert.AreEqual(0x1FF00, back.Payload.Length);
        }

        [Test]
        public void MaximumSizeFrameIsAccepted()
        {
            Frame frame = new Frame(1, 0x0B, 1, new byte[Frame.MaxPayloadLength]);
            byte[] wire = Encode(frame);
            Assert.AreEqual(4 + ProtocolInfo.MaxFrameLength, wire.Length);
            Assert.AreEqual(Frame.MaxPayloadLength, Decode(wire).Payload.Length);
        }

        [Test]
        public void PayloadOverTheLimitCannotBeBuilt()
        {
            Assert.Throws<ProtocolException>(delegate { new Frame(1, 1, 1, new byte[Frame.MaxPayloadLength + 1]); });
        }

        [Test]
        public void OversizedLengthIsRejectedBeforeReadingTheBody()
        {
            // 1 MiB + 1; the stream holds no body, so success would mean the length was not checked first.
            byte[] wire = new byte[] { 0x01, 0x00, 0x10, 0x00, 0, 0, 0, 0, 0, 0 };
            Assert.Throws<ProtocolException>(delegate { Decode(wire); });
        }

        [Test]
        public void HugeLengthIsRejected()
        {
            byte[] wire = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0 };
            Assert.Throws<ProtocolException>(delegate { Decode(wire); });
        }

        [Test]
        public void LengthSmallerThanTheHeaderIsRejected()
        {
            for (byte length = 0; length < 6; length++)
            {
                byte[] wire = new byte[4 + length];
                wire[0] = length;
                Assert.Throws<ProtocolException>(delegate { Decode(wire); }, "length " + length);
            }
        }

        [Test]
        public void TruncatedLengthFieldIsRejected()
        {
            Assert.Throws<ProtocolException>(delegate { Decode(new byte[] { 6, 0 }); });
        }

        [Test]
        public void TruncatedBodyIsRejectedAtEveryCutPoint()
        {
            byte[] wire = Encode(new Frame(1, 2, 3, new byte[] { 1, 2, 3, 4, 5 }));
            for (int cut = 1; cut < wire.Length; cut++)
            {
                byte[] part = new byte[cut];
                Array.Copy(wire, part, cut);
                Assert.Throws<ProtocolException>(delegate { Decode(part); }, "cut at " + cut);
            }
        }

        [Test]
        public void EmptyStreamMeansCleanEndOfConnection()
        {
            Assert.IsNull(Decode(new byte[0]));
        }

        [Test]
        public void SeveralFramesAreReadInOrderThenNull()
        {
            MemoryStream ms = new MemoryStream();
            FrameStream.Write(ms, new Frame(0, 3, 1, null));
            FrameStream.Write(ms, new Frame(1, 8, 2, new byte[] { 9 }));
            FrameStream.Write(ms, new Frame(0, 4, 3, null));
            ms.Position = 0;

            Assert.AreEqual(1u, FrameStream.Read(ms).RequestId);
            Assert.AreEqual(2u, FrameStream.Read(ms).RequestId);
            Assert.AreEqual(3u, FrameStream.Read(ms).RequestId);
            Assert.IsNull(FrameStream.Read(ms));
        }

        [Test]
        public void FramesSplitAcrossManySmallReadsAreReassembled()
        {
            Frame frame = new Frame(1, 0x0B, 77, Samples.Bytes(300));
            Frame back = FrameStream.Read(new TrickleStream(Encode(frame)));
            Assert.AreEqual(77u, back.RequestId);
            Assert.AreEqual(frame.Payload, back.Payload);
        }

        [Test]
        public void WriteIssuesASingleWriteCall()
        {
            CountingStream stream = new CountingStream();
            FrameStream.Write(stream, new Frame(1, 2, 3, new byte[100]));
            Assert.AreEqual(1, stream.WriteCalls);
            Assert.AreEqual(110, stream.Length);
        }

        private sealed class CountingStream : MemoryStream
        {
            public int WriteCalls;

            public override void Write(byte[] buffer, int offset, int count)
            {
                WriteCalls++;
                base.Write(buffer, offset, count);
            }
        }
    }
}
