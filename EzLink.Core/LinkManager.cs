using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>Timing and port settings of the link; the defaults are the plan's.</summary>
    public sealed class LinkOptions
    {
        public DiscoveryOptions Discovery = new DiscoveryOptions();
        /// <summary>TCP port this machine listens on (and announces in its beacon).</summary>
        public ushort ListenPort = Constants.SessionPort;
        public TimeSpan PingInterval = TimeSpan.FromSeconds(5);
        public TimeSpan PingTimeout = TimeSpan.FromSeconds(15);
        /// <summary>The higher-UUID side waits this long for the other side to connect before connecting itself.</summary>
        public TimeSpan HigherSideFallback = TimeSpan.FromSeconds(10);
        public TimeSpan ConnectRetry = TimeSpan.FromSeconds(1);
        /// <summary>How often a listener that could not bind (port in use) is retried while a peer is present.</summary>
        public TimeSpan ListenerRetry = TimeSpan.FromSeconds(2);
        /// <summary>Never open the control session, only accept one. For tests of the other side's fallback.</summary>
        public bool DisableConnecting;
    }

    /// <summary>
    /// Runs the link: discovery, the TCP listener on the link adapter, the control session and the
    /// <see cref="LinkStateMachine"/>. The side with the numerically lower UUID opens the control session; the
    /// other side connects if nothing arrives within 10 seconds.
    /// <para>Events and state changes happen on background threads.</para>
    /// </summary>
    public sealed class LinkManager : ISessionHost, IDisposable
    {
        private readonly IAdapterSource source;
        private readonly Guid machineId;
        private readonly string computerName;
        private readonly string appVersion;
        private readonly LinkOptions options;
        private readonly DiscoveryService discovery;
        private readonly LinkStateMachine state = new LinkStateMachine();
        private readonly TrustManager trust;

        private readonly object sync = new object();
        private readonly object listenerSync = new object(); // serializes EnsureListener
        private Timer listenerRetryTimer;
        private readonly Dictionary<string, SessionListener> listeners = new Dictionary<string, SessionListener>();
        private PeerInfo peer;               // the peer whose beacon we hear
        private PeerSession control;         // the open control session
        private readonly List<PeerSession> fileSessions = new List<PeerSession>(); // sessions we serve
        private bool connectorRunning;
        private bool started;
        private readonly ManualResetEvent stopEvent = new ManualResetEvent(false);

        /// <summary>Raised when a control session with the peer opens.</summary>
        public event EventHandler<PeerEventArgs> ControlOpened;
        /// <summary>Raised when the control session closes.</summary>
        public event EventHandler<PeerEventArgs> ControlClosed;

        /// <summary>A link without a trust manager: the control session works, every other session is denied.</summary>
        public LinkManager(IAdapterSource source, Guid machineId, string computerName, string appVersion,
            LinkOptions options)
            : this(source, machineId, computerName, appVersion, options, null)
        {
        }

        public LinkManager(IAdapterSource source, Guid machineId, string computerName, string appVersion,
            LinkOptions options, TrustManager trust)
        {
            this.trust = trust;
            this.source = source;
            this.machineId = machineId;
            this.computerName = computerName;
            this.appVersion = appVersion;
            this.options = options != null ? options : new LinkOptions();
            discovery = new DiscoveryService(source, machineId, computerName, appVersion,
                this.options.ListenPort, this.options.Discovery);

            discovery.AdaptersChanged += OnAdaptersChanged;
            discovery.PeerFound += OnPeerFound;
            discovery.PeerChanged += OnPeerChanged;
            discovery.PeerLost += OnPeerLost;
            discovery.DuplicateMachineId += OnDuplicateMachineId;
            discovery.ChannelError += OnChannelError;
            discovery.ChannelErrorsCleared += OnChannelErrorsCleared;
            if (trust != null)
            {
                trust.PermissionsChanged += OnPermissionsChanged;
            }
        }

        public LinkStateMachine State
        {
            get { return state; }
        }

        public DiscoveryService Discovery
        {
            get { return discovery; }
        }

        public Guid MachineId
        {
            get { return machineId; }
        }

        /// <summary>The trust manager, or null if this link has none.</summary>
        public TrustManager Trust
        {
            get { return trust; }
        }

        public string ComputerName
        {
            get { return computerName; }
        }

        /// <summary>The peer we hear on the link (or null).</summary>
        public PeerInfo CurrentPeer
        {
            get { lock (sync) { return peer == null ? null : peer.Clone(); } }
        }

        /// <summary>The open control session (or null).</summary>
        public PeerSession ControlSession
        {
            get { lock (sync) { return control; } }
        }

        public void Start()
        {
            if (started)
            {
                return;
            }
            started = true;
            stopEvent.Reset();
            discovery.Start();
            listenerRetryTimer = new Timer(RetryListener, null, options.ListenerRetry, options.ListenerRetry);
        }

        /// <summary>Re-attempts a listener that failed to bind: the connector thread ends once a session is up.</summary>
        private void RetryListener(object state)
        {
            PeerInfo current = CurrentPeer;
            if (current != null && started)
            {
                EnsureListener(current);
            }
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }
            started = false;
            stopEvent.Set();
            if (listenerRetryTimer != null)
            {
                listenerRetryTimer.Dispose();
                listenerRetryTimer = null;
            }
            discovery.Stop();

            PeerSession session;
            List<SessionListener> toStop;
            List<PeerSession> served;
            lock (sync)
            {
                served = new List<PeerSession>(fileSessions);
                session = control;
                control = null;
                peer = null;
                toStop = new List<SessionListener>(listeners.Values);
                listeners.Clear();
            }
            if (session != null)
            {
                session.Close("shutting down");
            }
            foreach (PeerSession file in served)
            {
                file.Close("shutting down");
            }
            foreach (SessionListener listener in toStop)
            {
                listener.Stop();
            }
        }

        public void Dispose()
        {
            Stop();
            discovery.Dispose();
            stopEvent.Close();
            if (trust != null)
            {
                trust.PermissionsChanged -= OnPermissionsChanged;
            }
        }

        // ---- ISessionHost: the listening side ----

        public HelloReplyMessage HandleHello(HelloMessage hello, IPEndPoint remote, SessionConnection connection)
        {
            HelloReplyMessage reply = new HelloReplyMessage();
            reply.MachineId = machineId;
            reply.ComputerName = computerName;
            reply.AppVersion = appVersion;

            if (hello.ProtocolVersion != ProtocolInfo.Version)
            {
                Log.Warn("{0} speaks protocol {1}, we speak {2}", remote, hello.ProtocolVersion, ProtocolInfo.Version);
                reply.Status = HelloStatus.VersionMismatch;
            }
            else if (hello.MachineId == machineId)
            {
                Log.Warn("Rejecting a Hello that carries our own machine id from {0}", remote);
                reply.Status = HelloStatus.Denied;
            }
            else if (hello.SessionType == SessionTypes.Control)
            {
                // The control session is always accepted so both sides can show the peer's name and trust state.
                reply.Status = HelloStatus.Accepted;
            }
            else if (hello.SessionType != SessionTypes.Files || trust == null)
            {
                Log.Info("Refusing {0} session from {1}: not available", hello.SessionType, hello.ComputerName);
                reply.Status = HelloStatus.Denied;
            }
            else
            {
                AuthorizeSession(hello, remote, connection, reply);
                if (reply.Status == HelloStatus.Accepted)
                {
                    reply.Capabilities.Add(Capabilities.Files);
                }
            }
            return reply;
        }

        /// <summary>
        /// Applies the trust decision to a files session. If the user has to be asked, the peer is told to wait
        /// (PendingApproval) first; the prompt is abandoned if the connection drops meanwhile.
        /// </summary>
        private void AuthorizeSession(HelloMessage hello, IPEndPoint remote, SessionConnection connection,
            HelloReplyMessage reply)
        {
            ManualResetEvent gone = new ManualResetEvent(false);
            EventHandler<ConnectionClosedEventArgs> onClosed = delegate { gone.Set(); };
            connection.Closed += onClosed;
            try
            {
                Permissions granted;
                reply.Status = trust.Authorize(hello.MachineId, hello.ComputerName, hello.SessionType,
                    delegate
                    {
                        HelloReplyMessage pending = new HelloReplyMessage();
                        pending.MachineId = reply.MachineId;
                        pending.ComputerName = reply.ComputerName;
                        pending.AppVersion = reply.AppVersion;
                        pending.Status = HelloStatus.PendingApproval;
                        connection.Send(pending.ToFrame(1));
                    },
                    gone, out granted);
                reply.GrantedPermissions = granted;
                Log.Info("Session {0} from {1} ({2}): {3} {4}", hello.SessionType, hello.ComputerName, remote,
                    reply.Status, granted);
            }
            finally
            {
                connection.Closed -= onClosed;
                gone.Close();
            }
        }

        public bool SessionEstablished(PeerSession session)
        {
            if (session.SessionType == SessionTypes.Files)
            {
                return ServeFiles(session);
            }
            if (session.SessionType != SessionTypes.Control)
            {
                return false;
            }
            return RegisterControl(session);
        }

        // ---- serving files ----

        /// <summary>
        /// Starts the file server on an accepted files session. What the peer may do is looked up for every
        /// request, so edits in Options and the end of an Allow once grant apply immediately.
        /// </summary>
        private bool ServeFiles(PeerSession session)
        {
            Guid peerId = session.PeerId;
            TrustManager manager = trust;
            lock (sync)
            {
                // Like the control session, a files session needs the peer to be on the link right now.
                if (!started || peer == null || peer.MachineId != peerId)
                {
                    Log.Info("Dropping files session {0}: the peer is no longer on the link", session);
                    return false;
                }
                fileSessions.Add(session);
            }
            session.Closed += delegate
            {
                lock (sync)
                {
                    fileSessions.Remove(session);
                }
            };
            new FileServer(session, delegate { return manager.GetEffective(peerId); });
            Log.Info("Serving files to {0}", session);
            return true;
        }

        private void CloseFileSessions(Guid peerId, string reason)
        {
            List<PeerSession> toClose = new List<PeerSession>();
            lock (sync)
            {
                foreach (PeerSession session in fileSessions)
                {
                    if (session.PeerId == peerId)
                    {
                        toClose.Add(session);
                    }
                }
            }
            foreach (PeerSession session in toClose)
            {
                session.Close(reason);
            }
        }

        // ---- discovery events ----

        private void OnAdaptersChanged(object sender, EventArgs e)
        {
            state.SetAdapters(discovery.Candidates);
        }

        private void OnPeerFound(object sender, PeerEventArgs e)
        {
            Log.Info("Link: peer {0} found", e.Peer);
            lock (sync)
            {
                peer = e.Peer;
            }
            state.SetPeer(e.Peer.ComputerName);
            EnsureListener(e.Peer);
            StartConnector();
        }

        private void OnPeerChanged(object sender, PeerEventArgs e)
        {
            lock (sync)
            {
                if (peer == null || peer.MachineId != e.Peer.MachineId)
                {
                    return;
                }
                peer = e.Peer;
            }
            state.SetPeer(e.Peer.ComputerName);
            EnsureListener(e.Peer);
        }

        private void OnPeerLost(object sender, PeerEventArgs e)
        {
            PeerSession session = null;
            SessionListener listener = null;
            // listenerSync first: a concurrent EnsureListener must either finish before the peer is cleared or
            // see that the peer is gone, never create a listener for a peer that has just been lost.
            lock (listenerSync)
            {
                lock (sync)
                {
                    if (peer == null || peer.MachineId != e.Peer.MachineId)
                    {
                        return;
                    }
                    Log.Info("Link: peer {0} lost", e.Peer);
                    peer = null;
                    session = control;
                    string key = e.Peer.LocalAddress.ToString();
                    if (listeners.TryGetValue(key, out listener))
                    {
                        listeners.Remove(key);
                    }
                }
            }
            state.SetPeer(null);
            CloseFileSessions(e.Peer.MachineId, "the peer is no longer on the link");
            if (trust != null)
            {
                trust.ClearOnce(e.Peer.MachineId); // Allow once ends when the cable is unplugged
            }
            if (listener != null)
            {
                listener.Stop();
            }
            if (session != null)
            {
                session.Close("the peer is no longer on the link");
            }
        }

        private void OnDuplicateMachineId(object sender, DuplicateMachineIdEventArgs e)
        {
            state.SetError(LinkErrorKind.DuplicateUuid, string.Format(Strings.Get("ErrorDuplicateUuid"), e.From));
        }

        private void OnChannelError(object sender, ChannelErrorEventArgs e)
        {
            state.SetError(LinkErrorKind.PortInUse, string.Format(Strings.Get("ErrorPortInUse"), e.Message));
        }

        private void OnChannelErrorsCleared(object sender, EventArgs e)
        {
            state.ClearError(LinkErrorKind.PortInUse);
        }

        // ---- the listener: opened only on the adapter where a peer actually answered ----

        private void EnsureListener(PeerInfo forPeer)
        {
            lock (listenerSync)
            {
                EnsureListenerLocked(forPeer);
            }
        }

        private void EnsureListenerLocked(PeerInfo forPeer)
        {
            string key = forPeer.LocalAddress.ToString();
            lock (sync)
            {
                bool stillCurrent = started && peer != null && peer.MachineId == forPeer.MachineId;
                if (!stillCurrent || listeners.ContainsKey(key))
                {
                    return;
                }
            }

            IPAddress mask = FindMask(forPeer);
            SessionListener listener = new SessionListener(forPeer.LocalAddress, mask, options.ListenPort, machineId, this);
            try
            {
                listener.Start();
            }
            catch (SocketException ex)
            {
                Log.Error("Cannot listen on " + listener, ex);
                LinkErrorKind kind = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                    || ex.SocketErrorCode == SocketError.AccessDenied
                    ? LinkErrorKind.PortInUse : LinkErrorKind.ListenFailed;
                string template = Strings.Get(kind == LinkErrorKind.PortInUse ? "ErrorPortInUse" : "ErrorListenFailed");
                state.SetError(kind, string.Format(template, ex.Message));
                return;
            }

            lock (sync)
            {
                listeners[key] = listener;
            }
            state.ClearError(LinkErrorKind.PortInUse);
            state.ClearError(LinkErrorKind.ListenFailed);
            Log.Info("Listening for sessions on {0}", listener);
        }

        private IPAddress FindMask(PeerInfo forPeer)
        {
            foreach (AdapterInfo adapter in discovery.Candidates)
            {
                if (adapter.Id != forPeer.AdapterId)
                {
                    continue;
                }
                foreach (AdapterAddress address in adapter.Addresses)
                {
                    if (address.Address.Equals(forPeer.LocalAddress))
                    {
                        return address.Mask;
                    }
                }
            }
            return null;
        }

        // ---- the connecting side ----

        private void StartConnector()
        {
            lock (sync)
            {
                if (connectorRunning || peer == null || control != null || !started)
                {
                    return;
                }
                connectorRunning = true;
            }
            Thread thread = new Thread(ConnectLoop);
            thread.IsBackground = true;
            thread.Name = "ezLink control connector";
            thread.Start();
        }

        /// <summary>Compares the 16 UUID bytes in order, as the plan prescribes.</summary>
        public static int CompareIds(Guid a, Guid b)
        {
            byte[] x = a.ToByteArray();
            byte[] y = b.ToByteArray();
            for (int i = 0; i < 16; i++)
            {
                if (x[i] != y[i])
                {
                    return x[i] < y[i] ? -1 : 1;
                }
            }
            return 0;
        }

        private void ConnectLoop()
        {
            DateTime began = DateTime.UtcNow;
            int failures = 0;
            try
            {
                while (!stopEvent.WaitOne(0, false))
                {
                    PeerInfo target;
                    lock (sync)
                    {
                        if (peer == null || control != null)
                        {
                            return; // peer gone, or the other side connected to us
                        }
                        target = peer.Clone();
                    }

                    bool myTurn = !options.DisableConnecting
                        && (CompareIds(machineId, target.MachineId) < 0
                        || DateTime.UtcNow - began >= options.HigherSideFallback);
                    if (myTurn && TryConnect(target, ref failures))
                    {
                        return;
                    }
                    if (stopEvent.WaitOne(myTurn ? options.ConnectRetry : TimeSpan.FromMilliseconds(250), false))
                    {
                        return;
                    }
                }
            }
            finally
            {
                lock (sync)
                {
                    connectorRunning = false;
                }
            }
        }

        private bool TryConnect(PeerInfo target, ref int failures)
        {
            HelloMessage hello = new HelloMessage();
            hello.SessionType = SessionTypes.Control;
            hello.MachineId = machineId;
            hello.ComputerName = computerName;
            hello.AppVersion = appVersion;

            try
            {
                HelloReplyMessage reply;
                PeerSession session = SessionClient.Connect(target.LocalAddress,
                    new IPEndPoint(target.Address, target.TcpPort), hello, out reply);
                if (session == null)
                {
                    Log.Warn("{0} refused the control session: {1}", target.ComputerName, reply.Status);
                    if (reply.Status == HelloStatus.VersionMismatch)
                    {
                        state.SetError(LinkErrorKind.VersionMismatch,
                            string.Format(Strings.Get("ErrorVersionMismatch"), target.ComputerName));
                    }
                    return false;
                }

                state.ClearError(LinkErrorKind.VersionMismatch);
                if (RegisterControl(session))
                {
                    session.Start();
                    session.StartKeepAlive(options.PingInterval, options.PingTimeout);
                }
                return true;
            }
            catch (Exception ex)
            {
                // Typically "connection refused" while the other side's listener is not up yet.
                failures++;
                if (failures == 1 || failures % 10 == 0)
                {
                    Log.Info("Control connection to {0} not established yet ({1}): {2}", target, failures, ex.Message);
                }
                return false;
            }
        }

        // ---- the control session ----

        /// <summary>
        /// Makes the session the control session. If both sides connected at once there are two sessions;
        /// the one opened by the lower UUID wins on both machines. Returns false if this session lost.
        /// </summary>
        private bool RegisterControl(PeerSession session)
        {
            PeerSession replaced = null;
            lock (sync)
            {
                // A handshake that was still in flight when the peer left the link (cable unplugged, adapter
                // gone) must not resurrect a session. Listeners only exist while a peer is current, so a session
                // with no current peer is always stale.
                if (!started || peer == null || peer.MachineId != session.PeerId)
                {
                    Log.Info("Dropping stale control session {0}: the peer is no longer on the link", session);
                    session.Close("stale: the peer is no longer on the link");
                    return false;
                }

                if (control != null && !control.IsClosed)
                {
                    bool newWins = CompareIds(session.InitiatorId, control.InitiatorId) < 0;
                    if (!newWins)
                    {
                        Log.Info("Dropping duplicate control session {0}", session);
                        session.Close("duplicate control session");
                        return false;
                    }
                    replaced = control;
                }
                control = session;
            }

            if (replaced != null)
            {
                Log.Info("Replacing control session {0} with {1}", replaced, session);
                replaced.Closed -= OnControlClosed;
                replaced.Close("replaced by a duplicate control session");
            }

            session.Closed += OnControlClosed;
            session.TrustChanged += OnTrustChanged;
            if (!session.Initiator)
            {
                // Accepted sessions are started by the listener; keep-alive must run on both sides.
                session.StartKeepAlive(options.PingInterval, options.PingTimeout);
            }

            discovery.SetSessionOpen(session.PeerId, true);
            state.SetControl(true, session.PeerName);
            state.SetPeerAllows(session.PeerGrantsUs);
            AnnounceWhatWeAllow(session);
            Log.Info("Control session open: {0}", session);

            EventHandler<PeerEventArgs> opened = ControlOpened;
            if (opened != null)
            {
                opened(this, new PeerEventArgs(PeerFromSession(session)));
            }
            return true;
        }

        /// <summary>Tells the peer (and the UI) what this machine currently lets the peer do.</summary>
        private void AnnounceWhatWeAllow(PeerSession session)
        {
            Permissions allowed = trust != null ? trust.GetEffective(session.PeerId) : Permissions.None;
            state.SetWeAllow(allowed);
            TrustChangedMessage message = new TrustChangedMessage();
            message.Granted = allowed;
            session.Send(message, 0);
        }

        private void OnPermissionsChanged(object sender, TrustEventArgs e)
        {
            PeerSession session;
            lock (sync)
            {
                session = control;
            }
            if (session != null && session.PeerId == e.PeerId && !session.IsClosed)
            {
                AnnounceWhatWeAllow(session);
            }
        }

        private void OnTrustChanged(object sender, EventArgs e)
        {
            PeerSession session = (PeerSession)sender;
            lock (sync)
            {
                if (session != control)
                {
                    return;
                }
            }
            state.SetPeerAllows(session.PeerGrantsUs);
        }

        private void OnControlClosed(object sender, SessionClosedEventArgs e)
        {
            PeerSession session = (PeerSession)sender;
            bool current;
            lock (sync)
            {
                current = session == control;
                if (current)
                {
                    control = null;
                }
            }
            if (!current)
            {
                Log.Info("Superseded control session closed: {0} ({1})", session, e.Reason);
                return;
            }

            Log.Info("Control session closed: {0} ({1})", session, e.Reason);
            discovery.SetSessionOpen(session.PeerId, false);
            state.SetControl(false, null);
            state.SetWeAllow(Permissions.None);
            CloseFileSessions(session.PeerId, "the control session ended");
            if (trust != null)
            {
                trust.ClearOnce(session.PeerId); // Allow once lasts for the lifetime of the control session
            }

            EventHandler<PeerEventArgs> closed = ControlClosed;
            if (closed != null)
            {
                closed(this, new PeerEventArgs(PeerFromSession(session)));
            }

            if (started)
            {
                StartConnector(); // the peer may still be beaconing: reconnect
            }
        }

        private static PeerInfo PeerFromSession(PeerSession session)
        {
            PeerInfo info = new PeerInfo();
            info.MachineId = session.PeerId;
            info.ComputerName = session.PeerName;
            info.AppVersion = session.PeerAppVersion;
            info.Address = session.Connection.RemoteEndPoint.Address;
            info.LocalAddress = session.Connection.LocalEndPoint.Address;
            info.TcpPort = (ushort)session.Connection.RemoteEndPoint.Port;
            return info;
        }
    }
}
