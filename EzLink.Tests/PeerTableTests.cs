using System;
using System.Collections.Generic;
using System.Net;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class PeerTableTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);
        private static readonly IPAddress Peer = IPAddress.Parse("169.254.7.7");
        private static readonly IPAddress Local = IPAddress.Parse("169.254.1.1");

        private static Beacon BeaconFrom(Guid id, string name)
        {
            Beacon b = new Beacon();
            b.MachineId = id;
            b.ComputerName = name;
            b.AppVersion = "1.0.0";
            b.TcpPort = 47771;
            return b;
        }

        private static PeerUpdate Update(PeerTable table, Beacon b, IPAddress source, DateTime now)
        {
            PeerInfo snapshot;
            return table.Update(b, source, "{A}", Local, now, out snapshot);
        }

        [Test]
        public void FirstBeaconAddsThePeer()
        {
            PeerTable table = new PeerTable();
            PeerInfo snapshot;
            PeerUpdate result = table.Update(BeaconFrom(Samples.Id, "ANA"), Peer, "{A}", Local, T0, out snapshot);

            Assert.AreEqual(PeerUpdate.Added, result);
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("ANA", snapshot.ComputerName);
            Assert.AreEqual(Peer, snapshot.Address);
            Assert.AreEqual(47771, snapshot.TcpPort);
            Assert.AreEqual("{A}", snapshot.AdapterId);
            Assert.AreEqual(T0, snapshot.FirstSeenUtc);
        }

        [Test]
        public void RepeatedBeaconsOnlyRefreshTheTimestamp()
        {
            PeerTable table = new PeerTable();
            Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0);
            Assert.AreEqual(PeerUpdate.Refreshed, Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0.AddSeconds(2)));
            Assert.AreEqual(T0.AddSeconds(2), table.Get(Samples.Id).LastSeenUtc);
            Assert.AreEqual(T0, table.Get(Samples.Id).FirstSeenUtc);
        }

        [Test]
        public void ChangedAddressPortOrNameIsReported()
        {
            PeerTable table = new PeerTable();
            Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0);

            Assert.AreEqual(PeerUpdate.Changed, Update(table, BeaconFrom(Samples.Id, "ANA"), IPAddress.Parse("169.254.9.9"), T0));
            Assert.AreEqual(PeerUpdate.Changed, Update(table, BeaconFrom(Samples.Id, "ANA-RENAMED"), IPAddress.Parse("169.254.9.9"), T0));

            Beacon otherPort = BeaconFrom(Samples.Id, "ANA-RENAMED");
            otherPort.TcpPort = 5000;
            Assert.AreEqual(PeerUpdate.Changed, Update(table, otherPort, IPAddress.Parse("169.254.9.9"), T0));
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(5000, table.Get(Samples.Id).TcpPort);
        }

        [Test]
        public void PeerExpiresAfterSixSecondsWithoutABeacon()
        {
            PeerTable table = new PeerTable();
            Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0);

            Assert.AreEqual(0, table.Expire(T0.AddSeconds(6), Timeout).Count, "exactly at the timeout it is still present");
            List<PeerInfo> gone = table.Expire(T0.AddSeconds(6.1), Timeout);
            Assert.AreEqual(1, gone.Count);
            Assert.AreEqual("ANA", gone[0].ComputerName);
            Assert.AreEqual(0, table.Count);
        }

        [Test]
        public void ABeaconKeepsThePeerAlive()
        {
            PeerTable table = new PeerTable();
            Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0);
            Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0.AddSeconds(5));
            Assert.AreEqual(0, table.Expire(T0.AddSeconds(10), Timeout).Count);
            Assert.AreEqual(1, table.Expire(T0.AddSeconds(11.5), Timeout).Count);
        }

        [Test]
        public void AnOpenSessionPreventsExpiryUntilItCloses()
        {
            PeerTable table = new PeerTable();
            Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0);
            Assert.IsTrue(table.SetSessionOpen(Samples.Id, true));

            Assert.AreEqual(0, table.Expire(T0.AddMinutes(10), Timeout).Count);

            table.SetSessionOpen(Samples.Id, false);
            Assert.AreEqual(1, table.Expire(T0.AddMinutes(10), Timeout).Count);
        }

        [Test]
        public void SessionFlagForAnUnknownPeerIsIgnored()
        {
            Assert.IsFalse(new PeerTable().SetSessionOpen(Guid.NewGuid(), true));
        }

        [Test]
        public void RemovingAChannelDropsOnlyItsPeers()
        {
            PeerTable table = new PeerTable();
            PeerInfo snapshot;
            Guid second = Guid.NewGuid();
            table.Update(BeaconFrom(Samples.Id, "ANA"), Peer, "{A}", Local, T0, out snapshot);
            table.Update(BeaconFrom(second, "BOB"), Peer, "{B}", IPAddress.Parse("10.0.0.2"), T0, out snapshot);

            List<PeerInfo> gone = table.RemoveByChannel("{A}", Local);

            Assert.AreEqual(1, gone.Count);
            Assert.AreEqual("ANA", gone[0].ComputerName);
            Assert.AreEqual(1, table.Count);
            Assert.IsNotNull(table.Get(second));
        }

        [Test]
        public void SnapshotsAreCopies()
        {
            PeerTable table = new PeerTable();
            Update(table, BeaconFrom(Samples.Id, "ANA"), Peer, T0);
            PeerInfo copy = table.Get(Samples.Id);
            copy.ComputerName = "tampered";
            Assert.AreEqual("ANA", table.Get(Samples.Id).ComputerName);
            Assert.IsNull(table.Get(Guid.NewGuid()));
        }
    }
}
