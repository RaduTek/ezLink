using System;
using System.Text;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class PayloadTests
    {
        [Test]
        public void IntegersAreLittleEndian()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteUInt16(0x0102);
            w.WriteUInt32(0x03040506);
            w.WriteInt64(0x0708090A0B0C0D0EL);
            Assert.AreEqual(
                new byte[] { 0x02, 0x01, 0x06, 0x05, 0x04, 0x03, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08, 0x07 },
                w.ToArray());
        }

        [Test]
        public void StringsAreLengthPrefixedUtf8()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteString("h\u00e9");
            Assert.AreEqual(new byte[] { 3, 0, (byte)'h', 0xC3, 0xA9 }, w.ToArray());
        }

        [Test]
        public void StringRoundTripKeepsUnicode()
        {
            string text = "C:\\Users\\\u00c4na\\\u4e2d\u6587 \ud83d\ude00";
            PayloadWriter w = new PayloadWriter();
            w.WriteString(text);
            w.WriteString(string.Empty);
            PayloadReader r = new PayloadReader(w.ToArray());
            Assert.AreEqual(text, r.ReadString());
            Assert.AreEqual(string.Empty, r.ReadString());
            r.EnsureEnd();
        }

        [Test]
        public void NullStringIsWrittenAsEmpty()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteString(null);
            Assert.AreEqual(string.Empty, new PayloadReader(w.ToArray()).ReadString());
        }

        [Test]
        public void StringLongerThanUInt16IsRejected()
        {
            PayloadWriter w = new PayloadWriter();
            Assert.Throws<ProtocolException>(delegate { w.WriteString(new string('a', 65536)); });
        }

        [Test]
        public void StringOfMaximumLengthRoundTrips()
        {
            string text = new string('a', 65535);
            PayloadWriter w = new PayloadWriter();
            w.WriteString(text);
            Assert.AreEqual(text, new PayloadReader(w.ToArray()).ReadString());
        }

        [Test]
        public void StringLengthBeyondPayloadIsRejected()
        {
            PayloadReader r = new PayloadReader(new byte[] { 0xFF, 0xFF, (byte)'a' });
            Assert.Throws<ProtocolException>(delegate { r.ReadString(); });
        }

        [Test]
        public void InvalidUtf8IsRejected()
        {
            PayloadReader r = new PayloadReader(new byte[] { 2, 0, 0xC3, 0x28 });
            Assert.Throws<ProtocolException>(delegate { r.ReadString(); });
        }

        [Test]
        public void ReadingPastTheEndIsAProtocolException()
        {
            PayloadReader r = new PayloadReader(new byte[] { 1, 2, 3 });
            Assert.Throws<ProtocolException>(delegate { r.ReadUInt32(); });
            Assert.Throws<ProtocolException>(delegate { new PayloadReader(new byte[7]).ReadInt64(); });
            Assert.Throws<ProtocolException>(delegate { new PayloadReader(new byte[15]).ReadGuid(); });
            Assert.Throws<ProtocolException>(delegate { new PayloadReader(new byte[0]).ReadByte(); });
        }

        [Test]
        public void BooleansAcceptOnlyZeroAndOne()
        {
            Assert.IsFalse(new PayloadReader(new byte[] { 0 }).ReadBool());
            Assert.IsTrue(new PayloadReader(new byte[] { 1 }).ReadBool());
            PayloadReader r = new PayloadReader(new byte[] { 2 });
            Assert.Throws<ProtocolException>(delegate { r.ReadBool(); });
        }

        [Test]
        public void GuidUsesToByteArrayLayout()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteGuid(Samples.Id);
            Assert.AreEqual(Samples.Id.ToByteArray(), w.ToArray());
            Assert.AreEqual(Samples.Id, new PayloadReader(w.ToArray()).ReadGuid());
        }

        [Test]
        public void FileTimeRoundTripsAtFullPrecision()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteFileTime(Samples.Time);
            DateTime back = new PayloadReader(w.ToArray()).ReadFileTime();
            Assert.AreEqual(Samples.Time, back);
            Assert.AreEqual(DateTimeKind.Utc, back.Kind);
        }

        [Test]
        public void LocalTimeIsConvertedToUtcOnWrite()
        {
            DateTime local = Samples.Time.ToLocalTime();
            PayloadWriter w = new PayloadWriter();
            w.WriteFileTime(local);
            Assert.AreEqual(Samples.Time, new PayloadReader(w.ToArray()).ReadFileTime());
        }

        [Test]
        public void DatesBeforeTheFileTimeEpochBecomeZero()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteFileTime(DateTime.MinValue);
            Assert.AreEqual(new byte[8], w.ToArray());
        }

        [Test]
        public void InvalidFileTimesAreRejected()
        {
            PayloadWriter negative = new PayloadWriter();
            negative.WriteInt64(-1);
            Assert.Throws<ProtocolException>(delegate { new PayloadReader(negative.ToArray()).ReadFileTime(); });

            PayloadWriter huge = new PayloadWriter();
            huge.WriteInt64(long.MaxValue);
            Assert.Throws<ProtocolException>(delegate { new PayloadReader(huge.ToArray()).ReadFileTime(); });
        }

        [Test]
        public void NegativeSizesAreRejected()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteInt64(-5);
            Assert.Throws<ProtocolException>(delegate { new PayloadReader(w.ToArray()).ReadNonNegativeInt64(); });
        }

        [Test]
        public void MagicIsAsciiAndChecked()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteMagic("EZLS");
            Assert.AreEqual(Encoding.ASCII.GetBytes("EZLS"), w.ToArray());
            new PayloadReader(w.ToArray()).ExpectMagic("EZLS");
            Assert.Throws<ProtocolException>(delegate { new PayloadReader(w.ToArray()).ExpectMagic("EZLB"); });
            Assert.Throws<ArgumentException>(delegate { new PayloadWriter().WriteMagic("TOOLONG"); });
        }

        [Test]
        public void ElementCountCannotExceedRemainingBytes()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteUInt32(uint.MaxValue);
            w.WriteByte(0);
            PayloadReader r = new PayloadReader(w.ToArray());
            Assert.Throws<ProtocolException>(delegate { r.ReadCount(1); });

            PayloadWriter ok = new PayloadWriter();
            ok.WriteUInt32(2);
            ok.WriteRaw(new byte[10]);
            Assert.AreEqual(2, new PayloadReader(ok.ToArray()).ReadCount(5));
        }

        [Test]
        public void StringListRoundTripsAndChecksCount()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteStringList(new string[] { "files/1", "sync/1" });
            PayloadReader r = new PayloadReader(w.ToArray());
            Assert.AreEqual(new string[] { "files/1", "sync/1" }, r.ReadStringList().ToArray());

            PayloadReader forged = new PayloadReader(new byte[] { 0xFF, 0xFF });
            Assert.Throws<ProtocolException>(delegate { forged.ReadStringList(); });
        }

        [Test]
        public void ByteFieldRoundTrips()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteBytes(new byte[] { 9, 8, 7 });
            w.WriteBytes(null);
            PayloadReader r = new PayloadReader(w.ToArray());
            Assert.AreEqual(new byte[] { 9, 8, 7 }, r.ReadBytes());
            Assert.AreEqual(0, r.ReadBytes().Length);
        }

        [Test]
        public void EnsureEndDetectsTrailingBytes()
        {
            PayloadReader r = new PayloadReader(new byte[] { 1, 2 });
            r.ReadByte();
            Assert.Throws<ProtocolException>(delegate { r.EnsureEnd(); });
            r.ReadByte();
            r.EnsureEnd();
        }
    }
}
