using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class AdapterLocatorTests
    {
        private const string Guid1 = "{11111111-AAAA-BBBB-CCCC-111111111111}";
        private const string Guid2 = "{22222222-AAAA-BBBB-CCCC-222222222222}";
        private const string Guid3 = "{33333333-AAAA-BBBB-CCCC-333333333333}";

        private static AdapterInfo Nic(string id, string name, OperationalStatus status, string ip)
        {
            List<AdapterAddress> addresses = new List<AdapterAddress>();
            if (ip != null)
            {
                addresses.Add(new AdapterAddress(IPAddress.Parse(ip), IPAddress.Parse("255.255.0.0")));
            }
            return new AdapterInfo(id, name, name, status, addresses, null);
        }

        [Test]
        public void HardwareIdPrefixIsTheRtl8152()
        {
            Assert.AreEqual(@"usb\vid_0bda&pid_8152", Constants.AdapterHardwareIdPrefix);
        }

        [Test]
        public void DeviceIdMatchingIsCaseInsensitiveAndAllowsARevisionSuffix()
        {
            Assert.IsTrue(AdapterLocator.IsCandidateDeviceId(@"USB\VID_0BDA&PID_8152"));
            Assert.IsTrue(AdapterLocator.IsCandidateDeviceId(@"usb\vid_0bda&pid_8152"));
            Assert.IsTrue(AdapterLocator.IsCandidateDeviceId(@"USB\VID_0BDA&PID_8152&REV_2000"));
        }

        [Test]
        public void OtherDevicesAreNotCandidates()
        {
            Assert.IsFalse(AdapterLocator.IsCandidateDeviceId(null));
            Assert.IsFalse(AdapterLocator.IsCandidateDeviceId(string.Empty));
            Assert.IsFalse(AdapterLocator.IsCandidateDeviceId(@"USB\VID_0BDA&PID_8153"));
            Assert.IsFalse(AdapterLocator.IsCandidateDeviceId(@"USB\VID_0B95&PID_1790"));
            Assert.IsFalse(AdapterLocator.IsCandidateDeviceId(@"PCI\VEN_10EC&DEV_8168"));
            Assert.IsFalse(AdapterLocator.IsCandidateDeviceId(@"*USB\VID_0BDA&PID_8152"));
        }

        [Test]
        public void InterfacesAreJoinedWithTheRegistryOnTheGuidIgnoringCase()
        {
            Dictionary<string, string> registry = new Dictionary<string, string>();
            registry[Guid1.ToLowerInvariant()] = @"USB\VID_0BDA&PID_8152&REV_2000"; // registry spelling differs
            registry[Guid2] = @"PCI\VEN_8086&DEV_1502";

            List<AdapterInfo> nics = new List<AdapterInfo>();
            nics.Add(Nic(Guid1, "Ethernet 5", OperationalStatus.Up, "169.254.10.20"));
            nics.Add(Nic(Guid2, "Ethernet", OperationalStatus.Up, "192.168.1.5"));
            nics.Add(Nic(Guid3, "No registry entry", OperationalStatus.Up, "10.0.0.2"));

            List<AdapterInfo> candidates = AdapterLocator.SelectCandidates(registry, nics);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(Guid1, candidates[0].Id);
            Assert.AreEqual("Ethernet 5", candidates[0].Name);
            Assert.AreEqual(@"USB\VID_0BDA&PID_8152&REV_2000", candidates[0].MatchingDeviceId);
            Assert.AreEqual("169.254.10.20", candidates[0].Addresses[0].Address.ToString());
        }

        [Test]
        public void SeveralCandidatesAreAllReturned()
        {
            Dictionary<string, string> registry = new Dictionary<string, string>();
            registry[Guid1] = @"USB\VID_0BDA&PID_8152";
            registry[Guid2] = @"USB\VID_0BDA&PID_8152&REV_2000";

            List<AdapterInfo> nics = new List<AdapterInfo>();
            nics.Add(Nic(Guid1, "Ethernet 3", OperationalStatus.Up, "169.254.1.1"));
            nics.Add(Nic(Guid2, "Ethernet 4", OperationalStatus.Down, null));

            Assert.AreEqual(2, AdapterLocator.SelectCandidates(registry, nics).Count);
        }

        [Test]
        public void NoRegistryEntriesMeansNoCandidates()
        {
            List<AdapterInfo> nics = new List<AdapterInfo>();
            nics.Add(Nic(Guid1, "Ethernet", OperationalStatus.Up, "10.0.0.2"));
            Assert.AreEqual(0, AdapterLocator.SelectCandidates(new Dictionary<string, string>(), nics).Count);
        }

        [Test]
        public void OnlyUpAdaptersWithARealIPv4AddressAreUsable()
        {
            Assert.IsTrue(Nic(Guid1, "a", OperationalStatus.Up, "169.254.1.1").IsUsable);
            Assert.IsFalse(Nic(Guid1, "a", OperationalStatus.Down, "169.254.1.1").IsUsable);
            Assert.IsFalse(Nic(Guid1, "a", OperationalStatus.Up, null).IsUsable);
            Assert.IsFalse(Nic(Guid1, "a", OperationalStatus.Up, "0.0.0.0").IsUsable); // XP while waiting for DHCP
            Assert.IsFalse(Nic(Guid1, "a", OperationalStatus.NotPresent, "169.254.1.1").IsUsable);
        }

        [Test]
        public void SignatureChangesWithStatusAndAddress()
        {
            string up = Nic(Guid1, "a", OperationalStatus.Up, "169.254.1.1").Signature;
            Assert.AreNotEqual(up, Nic(Guid1, "a", OperationalStatus.Down, "169.254.1.1").Signature);
            Assert.AreNotEqual(up, Nic(Guid1, "a", OperationalStatus.Up, "169.254.1.2").Signature);
            Assert.AreEqual(up, Nic(Guid1, "a", OperationalStatus.Up, "169.254.1.1").Signature);
        }

        [Test]
        public void ReadingTheRealRegistryAndInterfacesDoesNotThrow()
        {
            // Smoke test against this machine; the result depends on the hardware, so only check it runs.
            Assert.IsNotNull(AdapterLocator.FindCandidates());
        }
    }
}
