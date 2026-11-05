using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>
    /// Trust over a real link: two link managers on loopback, the higher-UUID one serving with a scripted
    /// approval prompt, the other one asking for a files session.
    /// </summary>
    [TestFixture]
    public class TrustLinkTests
    {
        private const int UdpA = 49790;
        private const int UdpB = 49792;
        private const int TcpA = 49791;
        private const int TcpB = 49793;
        private const Permissions ReadAccess = Permissions.Browse | Permissions.Read;
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(8);

        private static readonly Guid LowId = new Guid(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
        private static readonly Guid HighId = new Guid(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 });

        private string directory;
        private string storePath;
        private readonly List<LinkManager> managers = new List<LinkManager>();
        private readonly List<PeerSession> sessions = new List<PeerSession>();

        [SetUp]
        public void CreateFolder()
        {
            directory = Path.Combine(Path.GetTempPath(), "ezlink-trustlink-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            storePath = Path.Combine(directory, "trust.xml");
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (PeerSession s in sessions)
            {
                s.Close("test over");
            }
            sessions.Clear();
            foreach (LinkManager m in managers)
            {
                m.Dispose();
            }
            managers.Clear();
            Directory.Delete(directory, true);
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

        private LinkManager client;  // the lower UUID: asks for access
        private LinkManager server;  // the higher UUID: decides
        private FakeAdapterSource clientSource;

        private void StartPair(ScriptedPrompt prompt)
        {
            clientSource = new FakeAdapterSource(UdpA, OperationalStatus.Up);
            client = new LinkManager(clientSource, LowId, "CLIENT", "1.0.0", Fast(UdpA, UdpB, TcpA));
            server = new LinkManager(new FakeAdapterSource(UdpB, OperationalStatus.Up), HighId, "SERVER", "1.0.0",
                Fast(UdpB, UdpA, TcpB), new TrustManager(new TrustStore(storePath), prompt));
            managers.Add(client);
            managers.Add(server);
            client.Start();
            server.Start();
            Assert.IsTrue(WaitUntil(delegate { return client.State.State == LinkState.Connected && server.State.State == LinkState.Connected; }),
                "both sides reach Connected: " + client.State.State + " / " + server.State.State);
        }

        private static bool WaitUntil(Predicate<object> condition)
        {
            DateTime until = DateTime.UtcNow + Wait;
            while (DateTime.UtcNow < until)
            {
                if (condition(null))
                {
                    return true;
                }
                Thread.Sleep(20);
            }
            return false;
        }

        /// <summary>Opens a files session from the client to the server; null if it was not accepted.</summary>
        private PeerSession OpenFiles(out HelloReplyMessage reply)
        {
            HelloMessage hello = new HelloMessage();
            hello.SessionType = SessionTypes.Files;
            hello.MachineId = LowId;
            hello.ComputerName = "CLIENT";
            hello.AppVersion = "1.0.0";
            PeerSession session = SessionClient.Connect(IPAddress.Loopback, new IPEndPoint(IPAddress.Loopback, TcpB), hello, out reply);
            if (session != null)
            {
                sessions.Add(session);
            }
            return session;
        }

        [Test]
        public void AFilesSessionNeedsApprovalAndGetsWhatWasChosen()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess);
            StartPair(prompt);

            HelloReplyMessage reply;
            Assert.IsNotNull(OpenFiles(out reply));
            Assert.AreEqual(HelloStatus.Accepted, reply.Status);
            Assert.AreEqual(ReadAccess, reply.GrantedPermissions);
            Assert.AreEqual(1, prompt.Count);
            Assert.AreEqual("CLIENT", prompt.Asked[0]);
        }

        [Test]
        public void TheControlSessionNeverNeedsApproval()
        {
            ScriptedPrompt prompt = new ScriptedPrompt();
            StartPair(prompt);
            Assert.AreEqual(0, prompt.Count);
        }

        [Test]
        public void ABlockedComputerStillGetsTheControlSession()
        {
            TrustEntry blocked = new TrustEntry();
            blocked.PeerId = LowId;
            blocked.ComputerName = "CLIENT";
            blocked.Decision = TrustDecision.Deny;
            new TrustStore(storePath).Set(blocked);

            ScriptedPrompt prompt = new ScriptedPrompt();
            StartPair(prompt);

            HelloReplyMessage reply;
            Assert.IsNull(OpenFiles(out reply));
            Assert.AreEqual(HelloStatus.Denied, reply.Status);
            Assert.AreEqual(0, prompt.Count, "no prompt for a blocked computer");
            Assert.AreEqual(LinkState.Connected, server.State.State, "the control session is unaffected");
        }

        [Test]
        public void DenyIsRememberedAcrossARestartOfTheServer()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.Deny, Permissions.None);
            StartPair(prompt);
            HelloReplyMessage reply;
            Assert.IsNull(OpenFiles(out reply));
            Assert.AreEqual(HelloStatus.Denied, reply.Status);

            // "Restart": a new trust store and manager read the same file.
            TrustManager restarted = new TrustManager(new TrustStore(storePath), new ScriptedPrompt());
            Permissions granted;
            Assert.AreEqual(HelloStatus.Denied,
                restarted.Authorize(LowId, "CLIENT", SessionTypes.Files, null, new ManualResetEvent(false), out granted));
        }

        [Test]
        public void AllowOnceEndsWhenTheCableIsUnplugged()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess);
            StartPair(prompt);
            HelloReplyMessage reply;
            Assert.IsNotNull(OpenFiles(out reply));
            Assert.AreEqual(ReadAccess, server.Trust.GetEffective(LowId));
            Assert.AreEqual(ReadAccess, server.State.Status.WeAllow);

            clientSource.RemoveAdapter(); // unplug the client's end
            client.Discovery.RefreshAdapters();
            client.Stop();               // and with it the beacons stop

            Assert.IsTrue(WaitUntil(delegate { return server.Trust.GetEffective(LowId) == Permissions.None; }),
                "the grant must end with the control session");
            Assert.IsTrue(WaitUntil(delegate { return server.State.Status.WeAllow == Permissions.None; }));
            Assert.AreEqual(0, new TrustStore(storePath).GetAll().Count, "an Allow once grant is never stored");
        }

        [Test]
        public void EachSideLearnsWhatTheOtherAllows()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, ReadAccess | Permissions.Write);
            StartPair(prompt);
            HelloReplyMessage reply;
            Assert.IsNotNull(OpenFiles(out reply));

            Permissions all = ReadAccess | Permissions.Write;
            Assert.AreEqual(all, server.State.Status.WeAllow);
            Assert.IsTrue(WaitUntil(delegate { return client.State.Status.PeerAllows == all; }),
                "the client is told through TrustChanged on the control session");
            Assert.AreEqual(Permissions.None, server.State.Status.PeerAllows, "approval is one-directional");
        }

        [Test]
        public void ChangingTheStoredEntryIsAnnouncedToThePeer()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, ReadAccess);
            StartPair(prompt);
            HelloReplyMessage reply;
            Assert.IsNotNull(OpenFiles(out reply));
            Assert.IsTrue(WaitUntil(delegate { return client.State.Status.PeerAllows == ReadAccess; }));

            TrustEntry entry = server.Trust.Store.Get(LowId);
            entry.Decision = TrustDecision.Deny;
            server.Trust.SetEntry(entry); // the user blocks the client in Options

            Assert.IsTrue(WaitUntil(delegate { return client.State.Status.PeerAllows == Permissions.None; }));
            Assert.AreEqual(Permissions.None, server.State.Status.WeAllow);
        }

        [Test]
        public void AnUnknownSessionTypeIsDenied()
        {
            StartPair(new ScriptedPrompt());
            HelloMessage hello = new HelloMessage();
            hello.SessionType = SessionTypes.Sync;
            hello.MachineId = LowId;
            hello.ComputerName = "CLIENT";
            hello.AppVersion = "1.0.0";
            HelloReplyMessage reply;
            Assert.IsNull(SessionClient.Connect(IPAddress.Loopback, new IPEndPoint(IPAddress.Loopback, TcpB), hello, out reply));
            Assert.AreEqual(HelloStatus.Denied, reply.Status);
        }
    }
}
