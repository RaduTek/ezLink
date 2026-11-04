using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>
    /// Runs real UDP discovery between service instances on 127.0.0.1 (tests may use loopback; production code
    /// may not). Two "machines" differ by UUID and port: each binds its own port and beacons to the other's.
    /// </summary>
    [TestFixture]
    public class DiscoveryServiceTests
    {
        private const int PortA = 49770;
        private const int PortB = 49772;
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

        private static readonly Guid IdA = new Guid("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid IdB = new Guid("00000000-0000-0000-0000-00000000000b");

        /// <summary>A scriptable adapter list; "local" means the loopback endpoint of the given own ports.</summary>
        private sealed class FakeSource : IAdapterSource
        {
            private readonly object sync = new object();
            private readonly List<int> ownPorts = new List<int>();
            private AdapterInfo adapter;

            public FakeSource(int ownPort, OperationalStatus status)
            {
                ownPorts.Add(ownPort);
                SetStatus(status);
            }

            public void SetStatus(OperationalStatus status)
            {
                List<AdapterAddress> addresses = new List<AdapterAddress>();
                addresses.Add(new AdapterAddress(IPAddress.Loopback, IPAddress.Parse("255.255.255.255")));
                lock (sync)
                {
                    adapter = new AdapterInfo("{FAKE-LOOP}", "Fake loopback", "Fake", status, addresses, "usb\\vid_0bda&pid_8152");
                }
            }

            public void RemoveAdapter()
            {
                lock (sync)
                {
                    adapter = null;
                }
            }

            /// <summary>Treat beacons from this port as coming from this machine.</summary>
            public void AddOwnPort(int port)
            {
                lock (sync)
                {
                    ownPorts.Add(port);
                }
            }

            public List<AdapterInfo> GetCandidates()
            {
                lock (sync)
                {
                    List<AdapterInfo> list = new List<AdapterInfo>();
                    if (adapter != null)
                    {
                        list.Add(adapter);
                    }
                    return list;
                }
            }

            public bool IsLocalEndpoint(IPEndPoint endpoint)
            {
                lock (sync)
                {
                    return endpoint.Address.Equals(IPAddress.Loopback) && ownPorts.Contains(endpoint.Port);
                }
            }
        }

        private readonly List<DiscoveryService> started = new List<DiscoveryService>();

        [TearDown]
        public void StopEverything()
        {
            foreach (DiscoveryService service in started)
            {
                service.Dispose();
            }
            started.Clear();
        }

        private static DiscoveryOptions Fast(int localPort, int remotePort)
        {
            DiscoveryOptions o = new DiscoveryOptions();
            o.BeaconInterval = TimeSpan.FromMilliseconds(100);
            o.TickInterval = TimeSpan.FromMilliseconds(20);
            o.DetectionInterval = TimeSpan.FromMilliseconds(150);
            o.MissedBeacons = 3;
            o.LocalPort = localPort;
            o.RemotePort = remotePort;
            return o;
        }

        private DiscoveryService Create(Guid id, string name, int localPort, int remotePort, FakeSource source)
        {
            DiscoveryService service = new DiscoveryService(source, id, name, "1.0.0",
                (ushort)(localPort + 1), Fast(localPort, remotePort));
            started.Add(service);
            return service;
        }

        private sealed class Signal
        {
            public readonly ManualResetEvent Event = new ManualResetEvent(false);
            public PeerInfo Peer;
            public int Count;

            public void OnPeer(object sender, PeerEventArgs e)
            {
                Peer = e.Peer;
                Count++;
                Event.Set();
            }

            public void OnSignal(object sender, EventArgs e)
            {
                Count++;
                Event.Set();
            }

            public bool WaitFor()
            {
                return Event.WaitOne(Wait, false);
            }

            public bool StaysQuietFor(int milliseconds)
            {
                return !Event.WaitOne(milliseconds, false);
            }
        }

        [Test]
        public void TwoComputersFindEachOther()
        {
            FakeSource sourceA = new FakeSource(PortA, OperationalStatus.Up);
            FakeSource sourceB = new FakeSource(PortB, OperationalStatus.Up);
            DiscoveryService a = Create(IdA, "COMPUTER-A", PortA, PortB, sourceA);
            DiscoveryService b = Create(IdB, "COMPUTER-B", PortB, PortA, sourceB);
            Signal foundByA = new Signal();
            Signal foundByB = new Signal();
            a.PeerFound += foundByA.OnPeer;
            b.PeerFound += foundByB.OnPeer;

            a.Start();
            b.Start();

            Assert.IsTrue(foundByA.WaitFor(), "A should find B");
            Assert.IsTrue(foundByB.WaitFor(), "B should find A");
            Assert.AreEqual(IdB, foundByA.Peer.MachineId);
            Assert.AreEqual("COMPUTER-B", foundByA.Peer.ComputerName);
            Assert.AreEqual("1.0.0", foundByA.Peer.AppVersion);
            Assert.AreEqual(PortB + 1, foundByA.Peer.TcpPort);
            Assert.AreEqual(IPAddress.Loopback, foundByA.Peer.Address);
            Assert.AreEqual("{FAKE-LOOP}", foundByA.Peer.AdapterId, "the adapter the peer answered on is the link");
            Assert.AreEqual(IdA, foundByB.Peer.MachineId);
            Assert.AreEqual(1, a.Peers.Count);
        }

        [Test]
        public void FoundIsRaisedOnceNotOnEveryBeacon()
        {
            DiscoveryService a = Create(IdA, "A", PortA, PortB, new FakeSource(PortA, OperationalStatus.Up));
            DiscoveryService b = Create(IdB, "B", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal foundByA = new Signal();
            a.PeerFound += foundByA.OnPeer;
            a.Start();
            b.Start();

            Assert.IsTrue(foundByA.WaitFor());
            Thread.Sleep(600); // many more beacons arrive
            Assert.AreEqual(1, foundByA.Count);
        }

        [Test]
        public void OwnBroadcastIsNotMistakenForAPeerOrADuplicate()
        {
            // Beacons go to our own port, so every beacon we send comes back to us.
            DiscoveryService alone = Create(IdA, "A", PortA, PortA, new FakeSource(PortA, OperationalStatus.Up));
            Signal found = new Signal();
            Signal duplicate = new Signal();
            alone.PeerFound += found.OnPeer;
            alone.DuplicateMachineId += delegate(object s, DuplicateMachineIdEventArgs e) { duplicate.OnSignal(s, e); };
            alone.Start();

            Assert.IsTrue(found.StaysQuietFor(800));
            Assert.IsTrue(duplicate.StaysQuietFor(1));
            Assert.AreEqual(0, alone.Peers.Count);
        }

        [Test]
        public void ABeaconFromAnotherAdapterOfTheSameMachineIsIgnored()
        {
            FakeSource sourceA = new FakeSource(PortA, OperationalStatus.Up);
            sourceA.AddOwnPort(PortB); // pretend B's socket is this machine's second adapter
            DiscoveryService a = Create(IdA, "A", PortA, PortB, sourceA);
            DiscoveryService b = Create(IdB, "B", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal found = new Signal();
            a.PeerFound += found.OnPeer;
            a.Start();
            b.Start();

            Assert.IsTrue(found.StaysQuietFor(800));
        }

        [Test]
        public void SameUuidOnTwoMachinesIsReportedAsDuplicate()
        {
            DiscoveryService a = Create(IdA, "CLONE-1", PortA, PortB, new FakeSource(PortA, OperationalStatus.Up));
            DiscoveryService b = Create(IdA, "CLONE-2", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal duplicate = new Signal();
            Signal found = new Signal();
            a.DuplicateMachineId += delegate(object s, DuplicateMachineIdEventArgs e) { duplicate.OnSignal(s, e); };
            a.PeerFound += found.OnPeer;
            a.Start();
            b.Start();

            Assert.IsTrue(duplicate.WaitFor(), "the duplicate UUID must be reported");
            Assert.IsTrue(found.StaysQuietFor(1), "a clone must never be treated as a peer");
        }

        [Test]
        public void PeerIsLostWhenItStopsBeaconing()
        {
            DiscoveryService a = Create(IdA, "A", PortA, PortB, new FakeSource(PortA, OperationalStatus.Up));
            DiscoveryService b = Create(IdB, "B", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal found = new Signal();
            Signal lost = new Signal();
            a.PeerFound += found.OnPeer;
            a.PeerLost += lost.OnPeer;
            a.Start();
            b.Start();
            Assert.IsTrue(found.WaitFor());

            b.Stop();

            Assert.IsTrue(lost.WaitFor(), "three missed beacons should drop the peer");
            Assert.AreEqual(IdB, lost.Peer.MachineId);
            Assert.AreEqual(0, a.Peers.Count);
        }

        [Test]
        public void PeerWithAnOpenSessionIsNotExpired()
        {
            DiscoveryService a = Create(IdA, "A", PortA, PortB, new FakeSource(PortA, OperationalStatus.Up));
            DiscoveryService b = Create(IdB, "B", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal found = new Signal();
            Signal lost = new Signal();
            a.PeerFound += found.OnPeer;
            a.PeerLost += lost.OnPeer;
            a.Start();
            b.Start();
            Assert.IsTrue(found.WaitFor());

            a.SetSessionOpen(IdB, true);
            b.Stop();
            Assert.IsTrue(lost.StaysQuietFor(1000), "kept while the session is open");
            Assert.AreEqual(1, a.Peers.Count);

            a.SetSessionOpen(IdB, false);
            Assert.IsTrue(lost.WaitFor(), "dropped once the session is gone and no beacons arrive");
        }

        [Test]
        public void UnpluggingTheAdapterDropsThePeerAndReportsTheChange()
        {
            FakeSource sourceA = new FakeSource(PortA, OperationalStatus.Up);
            DiscoveryService a = Create(IdA, "A", PortA, PortB, sourceA);
            DiscoveryService b = Create(IdB, "B", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal found = new Signal();
            Signal lost = new Signal();
            Signal adapters = new Signal();
            a.PeerFound += found.OnPeer;
            a.PeerLost += lost.OnPeer;
            a.Start();
            b.Start();
            Assert.IsTrue(found.WaitFor());
            a.AdaptersChanged += adapters.OnSignal;

            sourceA.RemoveAdapter();
            a.RefreshAdapters();

            Assert.IsTrue(lost.WaitFor(), "the peer goes with its adapter");
            Assert.IsTrue(adapters.WaitFor());
            Assert.AreEqual(0, a.Candidates.Count);
            Assert.AreEqual(0, a.Peers.Count);
        }

        [Test]
        public void ReplugFindsThePeerAgain()
        {
            FakeSource sourceA = new FakeSource(PortA, OperationalStatus.Up);
            DiscoveryService a = Create(IdA, "A", PortA, PortB, sourceA);
            DiscoveryService b = Create(IdB, "B", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal found = new Signal();
            Signal lost = new Signal();
            a.PeerFound += found.OnPeer;
            a.PeerLost += lost.OnPeer;
            a.Start();
            b.Start();
            Assert.IsTrue(found.WaitFor());

            sourceA.RemoveAdapter();
            a.RefreshAdapters();
            Assert.IsTrue(lost.WaitFor());

            found.Event.Reset();
            sourceA.SetStatus(OperationalStatus.Up);
            a.RefreshAdapters();
            Assert.IsTrue(found.WaitFor(), "the peer should be found again after the adapter returns");
            Assert.AreEqual(2, found.Count);
        }

        [Test]
        public void AnAdapterThatIsDownDoesNotBeaconAndIsStillACandidate()
        {
            FakeSource sourceA = new FakeSource(PortA, OperationalStatus.Down);
            DiscoveryService a = Create(IdA, "A", PortA, PortB, sourceA);
            DiscoveryService b = Create(IdB, "B", PortB, PortA, new FakeSource(PortB, OperationalStatus.Up));
            Signal foundByB = new Signal();
            b.PeerFound += foundByB.OnPeer;
            a.Start();
            b.Start();

            Assert.IsTrue(foundByB.StaysQuietFor(800), "a down adapter must not beacon");
            Assert.AreEqual(1, a.Candidates.Count, "but the cable is known to be present");
            Assert.IsFalse(a.Candidates[0].IsUsable);

            sourceA.SetStatus(OperationalStatus.Up);
            a.RefreshAdapters();
            Assert.IsTrue(foundByB.WaitFor(), "once it is up the beacons start");
        }

        [Test]
        public void APortThatCannotBeBoundIsReportedAndRetried()
        {
            Socket blocker = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                blocker.ExclusiveAddressUse = true;
                blocker.Bind(new IPEndPoint(IPAddress.Loopback, PortA));

                DiscoveryService a = Create(IdA, "A", PortA, PortB, new FakeSource(PortA, OperationalStatus.Up));
                Signal error = new Signal();
                a.ChannelError += delegate(object s, ChannelErrorEventArgs e) { error.OnSignal(s, e); };
                a.Start();

                Assert.IsTrue(error.WaitFor(), "an exclusive owner of the port must be reported");
                Thread.Sleep(500);
                Assert.AreEqual(1, error.Count, "the same error is not repeated on every retry");
            }
            finally
            {
                blocker.Close();
            }
        }

        [Test]
        public void StartAndStopAreIdempotent()
        {
            DiscoveryService a = Create(IdA, "A", PortA, PortB, new FakeSource(PortA, OperationalStatus.Up));
            a.Stop();
            a.Start();
            a.Start();
            a.Stop();
            a.Stop();
            a.Start();
            a.Stop();
        }
    }
}
