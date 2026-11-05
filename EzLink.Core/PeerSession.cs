using System;
using System.Collections.Generic;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>
    /// One feature area of the protocol (a command set). Each set is a handler class registered with the
    /// session, so new features add a handler instead of changing the dispatcher.
    /// </summary>
    public interface ICommandSet
    {
        /// <summary>The set number, e.g. <see cref="CommandSets.Files"/>.</summary>
        byte CommandSet { get; }

        /// <summary>The capability string announced in Hello, e.g. "files/1".</summary>
        string Capability { get; }

        void Handle(Frame frame, PeerSession session);
    }

    public sealed class SessionClosedEventArgs : EventArgs
    {
        public readonly string Reason;

        public SessionClosedEventArgs(string reason)
        {
            Reason = reason;
        }
    }

    public sealed class MessageEventArgs : EventArgs
    {
        public readonly Message Message;
        public readonly uint RequestId;

        public MessageEventArgs(Message message, uint requestId)
        {
            Message = message;
            RequestId = requestId;
        }
    }

    /// <summary>
    /// An established session with a peer (after Hello): dispatches received frames to the registered
    /// command sets, answers Ping, tracks the peer's TrustChanged announcements and, for the control
    /// session, runs the keep-alive.
    /// </summary>
    public sealed class PeerSession
    {
        private readonly SessionConnection connection;
        private readonly string sessionType;
        private readonly bool initiator;
        private readonly Guid localId;
        private readonly Guid peerId;
        private readonly string peerName;
        private readonly string peerAppVersion;
        private readonly List<string> peerCapabilities;
        private readonly Dictionary<byte, ICommandSet> sets = new Dictionary<byte, ICommandSet>();
        private readonly object stateLock = new object();
        private readonly ManualResetEvent closedEvent = new ManualResetEvent(false);
        private int nextRequestId;
        private DateTime lastReceivedUtc = DateTime.UtcNow;
        private Permissions peerGrantsUs = Permissions.None;
        private Permissions grantedToPeer = Permissions.None;
        private bool started;

        public event EventHandler<SessionClosedEventArgs> Closed;
        /// <summary>The peer announced a new set of permissions it grants us; read <see cref="PeerGrantsUs"/>.</summary>
        public event EventHandler TrustChanged;
        /// <summary>
        /// A general reply (Ok, Error, Unsupported) arrived. Command sets that issue requests listen to this
        /// and match <see cref="MessageEventArgs.RequestId"/> against their pending requests. Raised on the read thread.
        /// </summary>
        public event EventHandler<MessageEventArgs> ReplyReceived;
        /// <summary>The peer asked to stop a request (its id is in the Cancel message). Raised on the read thread.</summary>
        public event EventHandler<MessageEventArgs> CancelReceived;

        public PeerSession(SessionConnection connection, string sessionType, bool initiator, Guid localId,
            Guid peerId, string peerName, string peerAppVersion, List<string> peerCapabilities)
        {
            this.connection = connection;
            this.sessionType = sessionType;
            this.initiator = initiator;
            this.localId = localId;
            this.peerId = peerId;
            this.peerName = peerName;
            this.peerAppVersion = peerAppVersion;
            this.peerCapabilities = peerCapabilities;
            connection.Closed += OnConnectionClosed;
        }

        public SessionConnection Connection
        {
            get { return connection; }
        }

        public string SessionType
        {
            get { return sessionType; }
        }

        /// <summary>True if this side opened the connection.</summary>
        public bool Initiator
        {
            get { return initiator; }
        }

        public Guid LocalId
        {
            get { return localId; }
        }

        public Guid PeerId
        {
            get { return peerId; }
        }

        /// <summary>The UUID of whichever machine opened the connection.</summary>
        public Guid InitiatorId
        {
            get { return initiator ? localId : peerId; }
        }

        public string PeerName
        {
            get { return peerName; }
        }

        public string PeerAppVersion
        {
            get { return peerAppVersion; }
        }

        public List<string> PeerCapabilities
        {
            get { return new List<string>(peerCapabilities); }
        }

        public bool IsClosed
        {
            get { return connection.IsClosed; }
        }

        /// <summary>What this machine lets the peer do on this session (set by the host after the trust decision).</summary>
        public Permissions GrantedToPeer
        {
            get { lock (stateLock) { return grantedToPeer; } }
            set { lock (stateLock) { grantedToPeer = value; } }
        }

        /// <summary>What the peer last said it lets us do.</summary>
        public Permissions PeerGrantsUs
        {
            get { lock (stateLock) { return peerGrantsUs; } }
        }

        public override string ToString()
        {
            return sessionType + " session with " + peerName + " (" + connection.LocalEndPoint.Port + " -> "
                + connection.RemoteEndPoint.Port + ", opened by " + (initiator ? "us" : "them") + ")";
        }

        /// <summary>Registers a command set handler. Do this before <see cref="Start"/>.</summary>
        public void Register(ICommandSet commandSet)
        {
            sets[commandSet.CommandSet] = commandSet;
        }

        /// <summary>Starts receiving frames.</summary>
        public void Start()
        {
            if (started)
            {
                return;
            }
            started = true;
            connection.FrameReceived += OnFrame;
            connection.StartReading();
        }

        /// <summary>
        /// Starts the control-session keep-alive: a Ping every <paramref name="interval"/>, and the session
        /// is closed if nothing at all arrives for <paramref name="timeout"/>.
        /// </summary>
        public void StartKeepAlive(TimeSpan interval, TimeSpan timeout)
        {
            lock (stateLock)
            {
                lastReceivedUtc = DateTime.UtcNow;
            }
            Thread thread = new Thread(delegate() { KeepAliveLoop(interval, timeout); });
            thread.IsBackground = true;
            thread.Name = "ezLink keep-alive " + peerName;
            thread.Start();
        }

        public uint NextRequestId()
        {
            uint id;
            do
            {
                id = (uint)Interlocked.Increment(ref nextRequestId);
            }
            while (id == 0);
            return id;
        }

        /// <summary>Sends a message. Returns false if the connection is gone.</summary>
        public bool Send(Message message, uint requestId)
        {
            return Send(message.ToFrame(requestId));
        }

        public bool Send(Frame frame)
        {
            try
            {
                connection.Send(frame);
                return true;
            }
            catch (System.IO.IOException)
            {
                return false;
            }
        }

        public void Close(string reason)
        {
            connection.Close(reason);
        }

        private void KeepAliveLoop(TimeSpan interval, TimeSpan timeout)
        {
            while (!closedEvent.WaitOne(interval, false))
            {
                DateTime last;
                lock (stateLock)
                {
                    last = lastReceivedUtc;
                }
                if (DateTime.UtcNow - last > timeout)
                {
                    Log.Warn("No data from {0} for {1} s; closing the control session", peerName, timeout.TotalSeconds);
                    Close("keep-alive timeout");
                    return;
                }
                if (!Send(new PingMessage(), NextRequestId()))
                {
                    return;
                }
            }
        }

        private void OnConnectionClosed(object sender, ConnectionClosedEventArgs e)
        {
            closedEvent.Set();
            EventHandler<SessionClosedEventArgs> handler = Closed;
            if (handler != null)
            {
                handler(this, new SessionClosedEventArgs(e.Reason));
            }
        }

        private void OnFrame(object sender, FrameEventArgs e)
        {
            Frame frame = e.Frame;
            lock (stateLock)
            {
                lastReceivedUtc = DateTime.UtcNow;
            }

            if (frame.CommandSet == CommandSets.General)
            {
                HandleGeneral(frame);
                return;
            }

            ICommandSet handler;
            if (!sets.TryGetValue(frame.CommandSet, out handler))
            {
                ReplyUnsupported(frame);
                return;
            }
            handler.Handle(frame, this);
        }

        private void HandleGeneral(Frame frame)
        {
            Message message;
            try
            {
                message = MessageFactory.Parse(frame);
            }
            catch (ProtocolException ex)
            {
                Close("protocol error: " + ex.Message);
                return;
            }

            if (message == null)
            {
                ReplyUnsupported(frame);
            }
            else if (message is PingMessage)
            {
                Send(new PongMessage(), frame.RequestId);
            }
            else if (message is TrustChangedMessage)
            {
                lock (stateLock)
                {
                    peerGrantsUs = ((TrustChangedMessage)message).Granted;
                }
                EventHandler handler = TrustChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            else if (message is OkMessage || message is ErrorMessage || message is UnsupportedMessage)
            {
                Raise(ReplyReceived, message, frame.RequestId);
            }
            else if (message is CancelMessage)
            {
                Raise(CancelReceived, message, ((CancelMessage)message).TargetRequestId);
            }
            else if (message is HelloMessage || message is HelloReplyMessage)
            {
                Close("unexpected Hello after the handshake");
            }
            // Pong needs nothing beyond the timestamp update.
        }

        private void Raise(EventHandler<MessageEventArgs> handler, Message message, uint requestId)
        {
            if (handler != null)
            {
                handler(this, new MessageEventArgs(message, requestId));
            }
        }

        private void ReplyUnsupported(Frame frame)
        {
            Log.Warn("Unsupported command {0}/{1} from {2}", frame.CommandSet, frame.Command, peerName);
            Send(new UnsupportedMessage(frame.CommandSet, frame.Command), frame.RequestId);
        }
    }
}
