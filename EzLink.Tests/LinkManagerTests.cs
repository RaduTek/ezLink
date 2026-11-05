using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>
    /// Two complete link managers (discovery, listener, control session, state machine) talking over loopback.
    /// The timing constants are shortened but the logic is the production logic.
    /// </summary>
    [TestFixture]
    public class LinkManagerTests
    {
        private const int UdpA = 49770;
        private const int UdpB = 49772;
        private const int TcpA = 49771;
        private const int TcpB = 49773;
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(8);

        // Compare the 16 UUID bytes in order: LowId < HighId.
        private static readonly Guid LowId = new Guid(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
        private static readonly Guid HighId = new Guid(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 });

        /// <summary>Records every distinct state a state machine passes through.</summary>
        private sealed class Recorder
        {
            private readonly LinkStateMachine machine;
            public readonly List<LinkState> History = new List<LinkState>();

            public Recorder(LinkStateMachine machine)
            {
                this.machine = machine;
                History.Add(machine.State);
                machine.StateChanged += OnChanged;
            }

            private void OnChanged(object sender, EventArgs e)
            {
                lock (History)
                {
                    LinkState now = machine.State;
                    if (History[History.Count - 1] != now)
                    {
                        History.Add(now);
                    }
                }
            }

            public bool WaitFor(LinkState state)
            {
                DateTime until = DateTime.UtcNow + Wait;
                while (DateTime.UtcNow < until)
                {
                    if (machine.State == state)
                    {
                        return true;
                    }
                    Thread.Sleep(20);
                }
                return false;
            }

            public string Path
            {
                get
                {
                    lock (History)
                    {
                        string[] names = new string[History.Count];
                        for (int i = 0; i < names.Length; i++)
                        {
                            names[i] = History[i].ToString();
                        }
                        return string.Join(" > ", names);
                    }
                }
            }

            public void Forget()
            {
                lock (History)
                {
                    History.Clear();
                    History.Add(machine.State);
                }
            }
        }

        private readonly List<LinkManager> managers = new List<LinkManager>();

        [TearDown]
        public void StopEverything()
        {
            foreach (LinkManager m in managers)
            {
                m.Dispose();
            }
            managers.Clear();
        }

        private static LinkOptions Fast(int udpLocal, int udpRemote, int tcp)
        {
            LinkOptions o = new LinkOptions();
            o.ListenPort = (ushort)tcp;
            o.Discovery.BeaconInterval = TimeSpan.FromMilliseconds(100);
            o.Discovery.TickInterval = TimeSpan.FromMilliseconds(20);
            o.Discovery.DetectionInterval = TimeSpan.FromMilliseconds(150);
            o.Discovery.MissedBeacons = 3;
            o.Discovery.LocalPort = udpLocal;
            o.Discovery.RemotePort = udpRemote;
            o.PingInterval = TimeSpan.FromMilliseconds(100);
            o.PingTimeout = TimeSpan.FromMilliseconds(800);
            o.HigherSideFallback = TimeSpan.FromMilliseconds(600);
            o.ConnectRetry = TimeSpan.FromMilliseconds(100);
            o.ListenerRetry = TimeSpan.FromMilliseconds(200);
            return o;
        }

        private LinkManager Create(Guid id, string name, FakeAdapterSource source, LinkOptions options)
        {
            LinkManager manager = new LinkManager(source, id, name, "1.0.0", options);
            managers.Add(manager);
            return manager;
        }

        private LinkManager CreateA(FakeAdapterSource source, LinkOptions options)
        {
            return Create(LowId, "COMPUTER-A", source, options);
        }

        private LinkManager CreateB(FakeAdapterSource source, LinkOptions options)
        {
            return Create(HighId, "COMPUTER-B", source, options);
        }

        [Test]
        public void TwoComputersReachConnected()
        {
            LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), Fast(UdpA, UdpB, TcpA));
            LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
            Recorder ra = new Recorder(a.State);
            Recorder rb = new Recorder(b.State);

            a.Start();
            b.Start();

            Assert.IsTrue(ra.WaitFor(LinkState.Connected), "A: " + ra.Path);
            Assert.IsTrue(rb.WaitFor(LinkState.Connected), "B: " + rb.Path);
            Assert.AreEqual("Connected to COMPUTER-B", a.State.Status.Text);
            Assert.AreEqual("Connected to COMPUTER-A", b.State.Status.Text);
            Assert.AreEqual(HighId, a.ControlSession.PeerId);
            Assert.AreEqual(LowId, b.ControlSession.PeerId);
        }

        [Test]
        public void TheLowerUuidOpensTheControlSession()
        {
            LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), Fast(UdpA, UdpB, TcpA));
            LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
            Recorder ra = new Recorder(a.State);
            Recorder rb = new Recorder(b.State);
            a.Start();
            b.Start();
            Assert.IsTrue(ra.WaitFor(LinkState.Connected));
            Assert.IsTrue(rb.WaitFor(LinkState.Connected));

            Assert.IsTrue(a.ControlSession.Initiator, "the lower UUID connects");
            Assert.IsFalse(b.ControlSession.Initiator);
        }

        [Test]
        public void TheHigherSideConnectsIfTheLowerSideNeverDoes()
        {
            LinkOptions lazy = Fast(UdpA, UdpB, TcpA);
            lazy.DisableConnecting = true;
            LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), lazy);
            LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
            Recorder ra = new Recorder(a.State);
            Recorder rb = new Recorder(b.State);
            DateTime began = DateTime.UtcNow;
            a.Start();
            b.Start();

            Assert.IsTrue(rb.WaitFor(LinkState.Connected), "B: " + rb.Path);
            Assert.IsTrue(ra.WaitFor(LinkState.Connected), "A: " + ra.Path);
            Assert.GreaterOrEqual((DateTime.UtcNow - began).TotalMilliseconds, 500, "B waits for the fallback delay first");
            Assert.IsTrue(b.ControlSession.Initiator);
        }

        [Test]
        public void BothSidesConnectingAtOnceEndUpWithOneSharedSession()
        {
            LinkOptions oa = Fast(UdpA, UdpB, TcpA);
            LinkOptions ob = Fast(UdpB, UdpA, TcpB);
            oa.HigherSideFallback = TimeSpan.Zero; // both sides connect immediately
            ob.HigherSideFallback = TimeSpan.Zero;
            LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), oa);
            LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), ob);
            Recorder ra = new Recorder(a.State);
            Recorder rb = new Recorder(b.State);
            a.Start();
            b.Start();

            Assert.IsTrue(ra.WaitFor(LinkState.Connected));
            Assert.IsTrue(rb.WaitFor(LinkState.Connected));
            Thread.Sleep(1500); // let any duplicate settle, and outlast the 0.8 s ping timeout

            Assert.AreEqual(LinkState.Connected, a.State.State, "A: " + ra.Path);
            Assert.AreEqual(LinkState.Connected, b.State.State, "B: " + rb.Path);
            // Both machines must agree on the one session. If two sessions really coexisted, the lower UUID's
            // wins; if only one was ever created, either side may have opened it.
            Assert.AreEqual(a.ControlSession.InitiatorId, b.ControlSession.InitiatorId);
            Assert.IsFalse(a.ControlSession.IsClosed);
            Assert.IsFalse(b.ControlSession.IsClosed);
        }

        [Test]
        public void ConnectedSurvivesPastThePingTimeout()
        {
            LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), Fast(UdpA, UdpB, TcpA));
            LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
            Recorder ra = new Recorder(a.State);
            Recorder rb = new Recorder(b.State);
            a.Start();
            b.Start();
            Assert.IsTrue(ra.WaitFor(LinkState.Connected));
            Assert.IsTrue(rb.WaitFor(LinkState.Connected));
            ra.Forget();
            rb.Forget();

            Thread.Sleep(2000); // 2.5 ping timeouts

            Assert.AreEqual(LinkState.Connected, a.State.State, "A: " + ra.Path);
            Assert.AreEqual(LinkState.Connected, b.State.State, "B: " + rb.Path);
        }

        [Test]
        public void UnplugAndReplugGoesBackToConnected()
        {
            FakeAdapterSource sourceA = new FakeAdapterSource(UdpA, OperationalStatus.Up);
            LinkManager a = CreateA(sourceA, Fast(UdpA, UdpB, TcpA));
            LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
            Recorder ra = new Recorder(a.State);
            Recorder rb = new Recorder(b.State);
            a.Start();
            b.Start();
            Assert.IsTrue(ra.WaitFor(LinkState.Connected));
            Assert.IsTrue(rb.WaitFor(LinkState.Connected));
            ra.Forget();
            rb.Forget();

            // Unplug on A's side: A loses its adapter; B just stops hearing A.
            sourceA.RemoveAdapter();
            a.Discovery.RefreshAdapters();
            Assert.IsTrue(ra.WaitFor(LinkState.NoCable), "A: " + ra.Path);
            Assert.IsTrue(rb.WaitFor(LinkState.Searching), "B: " + rb.Path);

            // Replug.
            sourceA.SetStatus(OperationalStatus.Up);
            a.Discovery.RefreshAdapters();
            Assert.IsTrue(ra.WaitFor(LinkState.Connected), "A: " + ra.Path);
            Assert.IsTrue(rb.WaitFor(LinkState.Connected), "B: " + rb.Path);

            // After the unplug A walked the table's states in order, and the unplug itself went straight to NoCable.
            Assert.AreEqual("Connected > NoCable > Searching > Connecting > Connected", ra.Path);
        }

        [Test]
        public void ThePeerEndingTheSessionReturnsToSearchingWhenItsBeaconsStop()
        {
            LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), Fast(UdpA, UdpB, TcpA));
            LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
            Recorder ra = new Recorder(a.State);
            Recorder rb = new Recorder(b.State);
            a.Start();
            b.Start();
            Assert.IsTrue(ra.WaitFor(LinkState.Connected));
            Assert.IsTrue(rb.WaitFor(LinkState.Connected));

            b.Stop();

            Assert.IsTrue(ra.WaitFor(LinkState.Searching), "A: " + ra.Path);
        }

        [Test]
        public void ADownAdapterShowsStartingTheConnection()
        {
            FakeAdapterSource source = new FakeAdapterSource(UdpA, OperationalStatus.Down);
            LinkManager a = CreateA(source, Fast(UdpA, UdpB, TcpA));
            Recorder ra = new Recorder(a.State);
            a.Start();

            Assert.IsTrue(ra.WaitFor(LinkState.WaitingForNetwork), ra.Path);
            Assert.AreEqual("Starting the connection…", a.State.Status.Text);

            source.SetStatus(OperationalStatus.Up);
            a.Discovery.RefreshAdapters();
            Assert.IsTrue(ra.WaitFor(LinkState.Searching), ra.Path);
        }

        [Test]
        public void ACloneOfThisMachineShowsAnError()
        {
            LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), Fast(UdpA, UdpB, TcpA));
            LinkManager clone = Create(LowId, "CLONE", new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
            Recorder ra = new Recorder(a.State);
            a.Start();
            clone.Start();

            Assert.IsTrue(ra.WaitFor(LinkState.Error), ra.Path);
            Assert.AreEqual(LinkErrorKind.DuplicateUuid, a.State.Status.ErrorKind);
        }

        [Test]
        public void ASessionPortInUseIsAnErrorThatClearsWhenThePortIsFree()
        {
            Socket blocker = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                blocker.Bind(new IPEndPoint(IPAddress.Loopback, TcpA));
                blocker.Listen(1);

                LinkManager a = CreateA(new FakeAdapterSource(UdpA, OperationalStatus.Up), Fast(UdpA, UdpB, TcpA));
                LinkManager b = CreateB(new FakeAdapterSource(UdpB, OperationalStatus.Up), Fast(UdpB, UdpA, TcpB));
                Recorder ra = new Recorder(a.State);
                a.Start();
                b.Start();

                Assert.IsTrue(ra.WaitFor(LinkState.Error), ra.Path);
                Assert.AreEqual(LinkErrorKind.PortInUse, a.State.Status.ErrorKind);
            }
            finally
            {
                blocker.Close();
            }

            Recorder after = new Recorder(managers[0].State);
            Assert.IsTrue(after.WaitFor(LinkState.Connected), "the listener is retried once the port is free: " + after.Path);
        }
    }
}
