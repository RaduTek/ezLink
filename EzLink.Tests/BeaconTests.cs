using System;
using System.Text;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class BeaconTests
    {
        private static Beacon Sample()
        {
            Beacon b = new Beacon();
            b.MachineId = Samples.Id;
            b.TcpPort = 47771;
            b.ComputerName = "ANA-LAPTOP";
            b.AppVersion = "1.0.0";
            return b;
        }

        [Test]
        public void BeaconRoundTrips()
        {
            Beacon back = Beacon.Parse(Sample().ToBytes());
            Assert.AreEqual(1, back.Version);
            Assert.AreEqual(Samples.Id, back.MachineId);
            Assert.AreEqual(47771, back.TcpPort);
            Assert.AreEqual("ANA-LAPTOP", back.ComputerName);
            Assert.AreEqual("1.0.0", back.AppVersion);
        }

        [Test]
        public void BeaconLayoutMatchesThePlan()
        {
            byte[] bytes = Sample().ToBytes();
            Assert.AreEqual("EZLB", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.AreEqual(new byte[] { 1, 0 }, new byte[] { bytes[4], bytes[5] });
            byte[] id = new byte[16];
            Array.Copy(bytes, 6, id, 0, 16);
            Assert.AreEqual(Samples.Id.ToByteArray(), id);
            Assert.AreEqual(new byte[] { 0x9B, 0xBA }, new byte[] { bytes[22], bytes[23] }); // 47771 = 0xBA9B
        }

        [Test]
        public void BeaconIsRejectedWhenMalformed()
        {
            byte[] good = Sample().ToBytes();

            for (int cut = 0; cut < good.Length; cut++)
            {
                byte[] part = new byte[cut];
                Array.Copy(good, part, cut);
                Assert.Throws<ProtocolException>(delegate { Beacon.Parse(part); }, "cut at " + cut);
            }

            byte[] wrongMagic = (byte[])good.Clone();
            wrongMagic[3] = (byte)'X';
            Assert.Throws<ProtocolException>(delegate { Beacon.Parse(wrongMagic); });

            byte[] wrongVersion = (byte[])good.Clone();
            wrongVersion[4] = 2;
            Assert.Throws<ProtocolException>(delegate { Beacon.Parse(wrongVersion); });

            byte[] trailing = new byte[good.Length + 1];
            Array.Copy(good, trailing, good.Length);
            Assert.Throws<ProtocolException>(delegate { Beacon.Parse(trailing); });

            Assert.Throws<ProtocolException>(delegate { Beacon.Parse(null); });
        }

        [Test]
        public void BeaconOver512BytesIsRejectedBothWays()
        {
            Assert.Throws<ProtocolException>(delegate { Beacon.Parse(new byte[513]); });

            Beacon big = Sample();
            big.ComputerName = new string('n', 600);
            Assert.Throws<ProtocolException>(delegate { big.ToBytes(); });
        }

        [Test]
        public void BeaconOfExactly512BytesIsAccepted()
        {
            Beacon b = Sample();
            int fixedPart = Sample().ToBytes().Length - b.ComputerName.Length;
            b.ComputerName = new string('n', Beacon.MaxLength - fixedPart);
            byte[] bytes = b.ToBytes();
            Assert.AreEqual(Beacon.MaxLength, bytes.Length);
            Assert.AreEqual(b.ComputerName, Beacon.Parse(bytes).ComputerName);
        }
    }
}
