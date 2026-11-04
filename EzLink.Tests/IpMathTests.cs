using System.Net;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class IpMathTests
    {
        private static string Broadcast(string address, string mask)
        {
            return IpMath.GetBroadcastAddress(IPAddress.Parse(address),
                mask == null ? null : IPAddress.Parse(mask)).ToString();
        }

        [Test]
        public void LinkLocalSlash16()
        {
            Assert.AreEqual("169.254.255.255", Broadcast("169.254.12.34", "255.255.0.0"));
        }

        [Test]
        public void OtherMasks()
        {
            Assert.AreEqual("192.168.1.255", Broadcast("192.168.1.20", "255.255.255.0"));
            Assert.AreEqual("10.0.3.255", Broadcast("10.0.2.9", "255.255.252.0"));
            Assert.AreEqual("192.168.1.159", Broadcast("192.168.1.130", "255.255.255.224"));
        }

        [Test]
        public void HostMaskGivesTheAddressItself()
        {
            Assert.AreEqual("127.0.0.1", Broadcast("127.0.0.1", "255.255.255.255"));
        }

        [Test]
        public void UnknownMaskFallsBackToSlash16ForLinkLocalAndSlash24Otherwise()
        {
            Assert.AreEqual("169.254.255.255", Broadcast("169.254.12.34", null));
            Assert.AreEqual("192.168.5.255", Broadcast("192.168.5.7", null));
        }
    }
}
