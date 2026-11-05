using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>The TCP session layer over loopback (tests may use loopback; production code may not).</summary>
    [TestFixture]
    public class SessionTests
    {
        private const int Port = 49780;
        private static readonly Guid ServerId = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid ClientId = new Guid("00000000-0000-0000-0000-0000000000b2");
        private static readonly IPAddress Mask = IPAddress.Parse("255.255.255.255");

        private sealed class TestHost : ISessionHost
        {
            public readonly List<PeerSession> Sessions = new List<PeerSession>();
            public readonly ManualResetEvent Established = new ManualResetEvent(false);
            public HelloStatus Answer = HelloStatus.Accepted;
            public ICommandSet RegisterSet;
            public int HelloCount;

            public HelloReplyMessage HandleHello(HelloMessage hello, IPEndPoint remote, SessionConnection connection)
            {
                HelloCount++;
                HelloReplyMessage reply = new HelloReplyMessage();
                reply.MachineId = ServerId;
                reply.ComputerName = "SERVER";
                reply.AppVersion = "1.0.0";
                reply.Status = hello.ProtocolVersion != ProtocolInfo.Version ? HelloStatus.VersionMismatch : Answer;
                return reply;
            }

            public bool SessionEstablished(PeerSession session)
            {
                if (RegisterSet != null)
                {
                    session.Register(RegisterSet);
                }
                lock (Sessions)
                {
                    Sessions.Add(session);
                }
                Established.Set();
                return true;
            }

            public PeerSession First
            {
                get { lock (Sessions) { return Sessions[0]; } }
            }
        }

        private sealed class RecordingSet : ICommandSet
        {
            public readonly List<Frame> Received = new List<Frame>();
            public readonly ManualResetEvent Got = new ManualResetEvent(false);

            public byte CommandSet { get { return CommandSets.Files; } }
            public string Capability { get { return Capabilities.Files; } }

            public void Handle(Frame frame, PeerSession session)
            {
                lock (Received) { Received.Add(frame); }
                session.Send(new OkMessage(), frame.RequestId);
                Got.Set();
            }
        }

        private TestHost host;
        private SessionListener listener;
        private readonly List<PeerSession> clients = new List<PeerSession>();

        [SetUp]
        public void StartListener()
        {
            host = new TestHost();
            listener = new SessionListener(IPAddress.Loopback, Mask, Port, ServerId, host);
            listener.Start();
        }

        [TearDown]
        public void StopListener()
        {
            foreach (PeerSession client in clients)
            {
                client.Close("test over");
            }
            clients.Clear();
            listener.Stop();
            lock (host.Sessions)
            {
                foreach (PeerSession s in host.Sessions)
                {
                    s.Close("test over");
                }
            }
        }

        private static HelloMessage Hello(string type)
        {
            HelloMessage hello = new HelloMessage();
            hello.SessionType = type;
            hello.MachineId = ClientId;
            hello.ComputerName = "CLIENT";
            hello.AppVersion = "1.0.0";
            return hello;
        }

        private PeerSession Connect(HelloMessage hello, out HelloReplyMessage reply)
        {
            PeerSession session = SessionClient.Connect(IPAddress.Loopback, new IPEndPoint(IPAddress.Loopback, Port), hello, out reply);
            if (session != null)
            {
                clients.Add(session);
            }
            return session;
        }

        private static List<Frame> Capture(PeerSession session, ManualResetEvent signal)
        {
            List<Frame> frames = new List<Frame>();
            session.Connection.FrameReceived += delegate(object s, FrameEventArgs e)
            {
                lock (frames) { frames.Add(e.Frame); }
                signal.Set();
            };
            return frames;
        }

        [Test]
        public void HandshakeAcceptsAndExchangesIdentities()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);

            Assert.IsNotNull(client);
            Assert.AreEqual(HelloStatus.Accepted, reply.Status);
            Assert.AreEqual("SERVER", client.PeerName);
            Assert.AreEqual(ServerId, client.PeerId);
            Assert.IsTrue(client.Initiator);
            Assert.AreEqual(ClientId, client.InitiatorId);

            Assert.IsTrue(host.Established.WaitOne(3000, false));
            PeerSession server = host.First;
            Assert.AreEqual("CLIENT", server.PeerName);
            Assert.AreEqual(ClientId, server.PeerId);
            Assert.AreEqual(SessionTypes.Control, server.SessionType);
            Assert.IsFalse(server.Initiator);
            Assert.AreEqual(ClientId, server.InitiatorId);
        }

        [Test]
        public void ADeniedHelloGivesNoSession()
        {
            host.Answer = HelloStatus.Denied;
            HelloReplyMessage reply;
            Assert.IsNull(Connect(Hello(SessionTypes.Files), out reply));
            Assert.AreEqual(HelloStatus.Denied, reply.Status);
            Assert.AreEqual(0, host.Sessions.Count);
        }

        [Test]
        public void AnotherProtocolVersionIsAnsweredWithVersionMismatch()
        {
            HelloMessage hello = Hello(SessionTypes.Control);
            hello.ProtocolVersion = 2;
            HelloReplyMessage reply;
            Assert.IsNull(Connect(hello, out reply));
            Assert.AreEqual(HelloStatus.VersionMismatch, reply.Status);
        }

        [Test]
        public void PingIsAnsweredWithPongCarryingTheSameRequestId()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);
            ManualResetEvent got = new ManualResetEvent(false);
            List<Frame> frames = Capture(client, got);
            client.Start();

            client.Send(new PingMessage(), 4242);

            Assert.IsTrue(got.WaitOne(3000, false));
            lock (frames)
            {
                Assert.AreEqual(CommandSets.General, frames[0].CommandSet);
                Assert.AreEqual(GeneralCommands.Pong, frames[0].Command);
                Assert.AreEqual(4242u, frames[0].RequestId);
            }
        }

        [Test]
        public void ACommandFromAnUnknownSetGetsUnsupportedAndTheSessionStaysOpen()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);
            ManualResetEvent got = new ManualResetEvent(false);
            List<Frame> frames = Capture(client, got);
            client.Start();

            client.Send(new Frame(0x42, 0x99, 77, new byte[] { 1, 2, 3 }));

            Assert.IsTrue(got.WaitOne(3000, false));
            UnsupportedMessage unsupported;
            lock (frames)
            {
                unsupported = (UnsupportedMessage)MessageFactory.Parse(frames[0]);
                Assert.AreEqual(77u, frames[0].RequestId);
            }
            Assert.AreEqual(0x42, unsupported.UnsupportedSet);
            Assert.AreEqual(0x99, unsupported.UnsupportedCommand);

            got.Reset();
            client.Send(new PingMessage(), 5);
            Assert.IsTrue(got.WaitOne(3000, false), "the session must survive an unsupported command");
            Assert.IsFalse(client.IsClosed);
        }

        [Test]
        public void AnUnknownGeneralCommandGetsUnsupportedToo()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);
            ManualResetEvent got = new ManualResetEvent(false);
            List<Frame> frames = Capture(client, got);
            client.Start();

            client.Send(new Frame(CommandSets.General, 0x7E, 3, null));

            Assert.IsTrue(got.WaitOne(3000, false));
            lock (frames)
            {
                Assert.AreEqual(GeneralCommands.Unsupported, frames[0].Command);
            }
        }

        [Test]
        public void FramesOfARegisteredSetReachItsHandler()
        {
            RecordingSet set = new RecordingSet();
            host.RegisterSet = set;
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Files), out reply);
            ManualResetEvent got = new ManualResetEvent(false);
            List<Frame> frames = Capture(client, got);
            client.Start();

            client.Send(new GetRootsRequest(), 11);

            Assert.IsTrue(set.Got.WaitOne(3000, false));
            Assert.AreEqual(FileCommands.GetRoots, set.Received[0].Command);
            Assert.AreEqual(11u, set.Received[0].RequestId);
            Assert.IsTrue(got.WaitOne(3000, false));
            lock (frames)
            {
                Assert.AreEqual(GeneralCommands.Ok, frames[0].Command);
                Assert.AreEqual(11u, frames[0].RequestId);
            }
        }

        [Test]
        public void TrustChangedIsRecordedAndAnnounced()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);
            Assert.IsTrue(host.Established.WaitOne(3000, false));
            PeerSession server = host.First;
            ManualResetEvent changed = new ManualResetEvent(false);
            server.TrustChanged += delegate { changed.Set(); };
            server.Start();
            client.Start();

            TrustChangedMessage trust = new TrustChangedMessage();
            trust.Granted = Permissions.Browse | Permissions.Read;
            client.Send(trust, 0);

            Assert.IsTrue(changed.WaitOne(3000, false));
            Assert.AreEqual(Permissions.Browse | Permissions.Read, server.PeerGrantsUs);
        }

        [Test]
        public void KeepAliveKeepsAnAnsweringSessionOpen()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);
            Assert.IsTrue(host.Established.WaitOne(3000, false));
            PeerSession server = host.First;
            server.Start();
            client.Start();
            server.StartKeepAlive(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));
            client.StartKeepAlive(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));

            Thread.Sleep(1500);

            Assert.IsFalse(server.IsClosed);
            Assert.IsFalse(client.IsClosed);
        }

        [Test]
        public void KeepAliveClosesASessionWhosePeerGoesSilent()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);
            Assert.IsTrue(host.Established.WaitOne(3000, false));
            PeerSession server = host.First;
            ManualResetEvent closed = new ManualResetEvent(false);
            server.Closed += delegate { closed.Set(); };
            server.Start();
            // The client never starts reading, so it never answers a Ping.
            server.StartKeepAlive(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));

            Assert.IsTrue(closed.WaitOne(3000, false), "15 s of silence is shortened to 0.5 s here");
            Assert.IsTrue(server.IsClosed);
        }

        [Test]
        public void ClosedIsRaisedWhenThePeerDisconnects()
        {
            HelloReplyMessage reply;
            PeerSession client = Connect(Hello(SessionTypes.Control), out reply);
            Assert.IsTrue(host.Established.WaitOne(3000, false));
            PeerSession server = host.First;
            ManualResetEvent closed = new ManualResetEvent(false);
            server.Closed += delegate { closed.Set(); };
            server.Start();
            client.Start();

            client.Close("bye");

            Assert.IsTrue(closed.WaitOne(3000, false));
        }

        [Test]
        public void AFirstFrameThatIsNotHelloClosesTheConnection()
        {
            using (Socket raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                raw.Connect(new IPEndPoint(IPAddress.Loopback, Port));
                System.IO.MemoryStream ms = new System.IO.MemoryStream();
                FrameStream.Write(ms, new PingMessage().ToFrame(1));
                raw.Send(ms.ToArray());

                raw.ReceiveTimeout = 3000;
                byte[] buffer = new byte[16];
                Assert.AreEqual(0, raw.Receive(buffer), "the listener must close without answering");
            }
            Assert.AreEqual(0, host.HelloCount);
        }

        [Test]
        public void AnOversizedFrameClosesTheSession()
        {
            using (Socket raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                raw.Connect(new IPEndPoint(IPAddress.Loopback, Port));
                raw.Send(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F, 0, 0, 0, 0, 0, 0 });
                raw.ReceiveTimeout = 3000;
                try
                {
                    Assert.AreEqual(0, raw.Receive(new byte[16]));
                }
                catch (SocketException ex)
                {
                    // The listener closed with unread data, so the stack may reset instead of closing cleanly.
                    Assert.AreEqual(SocketError.ConnectionReset, ex.SocketErrorCode);
                }
            }
        }

        [Test]
        public void ASilentClientIsDroppedAfterTheHelloTimeout()
        {
            using (Socket raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                raw.Connect(new IPEndPoint(IPAddress.Loopback, Port));
                raw.ReceiveTimeout = 9000;
                DateTime began = DateTime.UtcNow;
                Assert.AreEqual(0, raw.Receive(new byte[16]), "closed by the listener after 5 s");
                Assert.Less((DateTime.UtcNow - began).TotalSeconds, 8);
            }
        }

        [Test]
        public void ConnectionsFromOutsideTheLinkSubnetAreRejected()
        {
            // The listener's subnet is 127.0.0.1/32, so a connection that comes from 127.0.0.2 is "off the link".
            HelloReplyMessage reply = null;
            bool failed = false;
            try
            {
                PeerSession session = SessionClient.Connect(IPAddress.Parse("127.0.0.2"),
                    new IPEndPoint(IPAddress.Loopback, Port), Hello(SessionTypes.Control), out reply);
                if (session != null)
                {
                    clients.Add(session);
                }
            }
            catch (Exception)
            {
                failed = true;
            }
            Assert.IsTrue(failed, "the listener must close the connection without a reply");
            Assert.AreEqual(0, host.HelloCount);
        }

        [Test]
        public void AListenerCannotBindToAPortThatIsInUse()
        {
            SessionListener second = new SessionListener(IPAddress.Loopback, Mask, Port, ServerId, host);
            Assert.Throws<SocketException>(delegate { second.Start(); });
        }

        [Test]
        public void SubnetMembership()
        {
            IPAddress net = IPAddress.Parse("169.254.10.20");
            IPAddress mask = IPAddress.Parse("255.255.0.0");
            Assert.IsTrue(IpMath.IsInSubnet(IPAddress.Parse("169.254.200.1"), net, mask));
            Assert.IsFalse(IpMath.IsInSubnet(IPAddress.Parse("169.253.200.1"), net, mask));
            Assert.IsFalse(IpMath.IsInSubnet(IPAddress.Parse("192.168.1.1"), net, mask));
            Assert.IsTrue(IpMath.IsInSubnet(IPAddress.Parse("169.254.0.7"), net, null), "unknown mask: link-local /16");
            Assert.IsFalse(IpMath.IsInSubnet(IPAddress.IPv6Loopback, net, mask));
        }
    }
}
