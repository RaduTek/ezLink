using System;
using System.Collections.Generic;
using System.Text;
using EzLink.Protocol;

namespace EzLink
{
    public enum LinkState
    {
        /// <summary>No candidate adapter present.</summary>
        NoCable,
        /// <summary>A candidate is present but down or without an IPv4 address.</summary>
        WaitingForNetwork,
        /// <summary>Adapter up and beaconing, no peer seen.</summary>
        Searching,
        /// <summary>Peer beacon seen, control session opening.</summary>
        Connecting,
        /// <summary>Control session open.</summary>
        Connected,
        /// <summary>Duplicate UUID, port in use or similar.</summary>
        Error
    }

    public enum LinkErrorKind
    {
        None,
        DuplicateUuid,
        PortInUse,
        ListenFailed,
        VersionMismatch
    }

    /// <summary>An immutable snapshot of the link for the UI.</summary>
    public sealed class LinkStatus
    {
        public readonly LinkState State;
        /// <summary>The peer's computer name, or empty when there is no peer.</summary>
        public readonly string PeerName;
        public readonly LinkErrorKind ErrorKind;
        public readonly string ErrorMessage;
        /// <summary>What this machine lets the peer do.</summary>
        public readonly Permissions WeAllow;
        /// <summary>What the peer lets this machine do.</summary>
        public readonly Permissions PeerAllows;

        public LinkStatus(LinkState state, string peerName, LinkErrorKind errorKind, string errorMessage,
            Permissions weAllow, Permissions peerAllows)
        {
            State = state;
            PeerName = peerName;
            ErrorKind = errorKind;
            ErrorMessage = errorMessage;
            WeAllow = weAllow;
            PeerAllows = peerAllows;
        }

        /// <summary>The status line from the plan's state table.</summary>
        public string Text
        {
            get
            {
                switch (State)
                {
                    case LinkState.NoCable: return Strings.Get("StateNoCable");
                    case LinkState.WaitingForNetwork: return Strings.Get("StateWaitingForNetwork");
                    case LinkState.Searching: return Strings.Get("StateSearching");
                    case LinkState.Connecting: return string.Format(Strings.Get("StateConnecting"), PeerName);
                    case LinkState.Connected: return string.Format(Strings.Get("StateConnected"), PeerName);
                    default: return ErrorMessage;
                }
            }
        }

        /// <summary>"You allow NAME to: ..." (only meaningful while connected).</summary>
        public string WeAllowText
        {
            get
            {
                return string.Format(Strings.Get("TrustWeAllow"), PeerName,
                    DescribePermissions(WeAllow, Strings.Get("ThisComputer")));
            }
        }

        /// <summary>"NAME allows you to: ..." (only meaningful while connected).</summary>
        public string PeerAllowsText
        {
            get
            {
                return string.Format(Strings.Get("TrustPeerAllows"), PeerName,
                    DescribePermissions(PeerAllows, PeerName));
            }
        }

        public static string DescribePermissions(Permissions permissions, string computer)
        {
            List<string> parts = new List<string>();
            bool browse = (permissions & Permissions.Browse) != 0;
            bool read = (permissions & Permissions.Read) != 0;
            if (browse && read)
            {
                parts.Add(string.Format(Strings.Get("PermBrowseAndCopy"), computer));
            }
            else if (browse)
            {
                parts.Add(string.Format(Strings.Get("PermBrowseOnly"), computer));
            }
            else if (read)
            {
                parts.Add(string.Format(Strings.Get("PermCopyFrom"), computer));
            }
            if ((permissions & Permissions.Write) != 0)
            {
                parts.Add(string.Format(Strings.Get("PermWrite"), computer));
            }
            return parts.Count == 0 ? Strings.Get("PermNone") : string.Join("; ", parts.ToArray());
        }

        public bool SameAs(LinkStatus other)
        {
            return other != null
                && State == other.State
                && PeerName == other.PeerName
                && ErrorKind == other.ErrorKind
                && ErrorMessage == other.ErrorMessage
                && WeAllow == other.WeAllow
                && PeerAllows == other.PeerAllows;
        }
    }

    /// <summary>
    /// Owns the connection state. Adapters, discovery, sessions and trust report their facts into it; the
    /// state is derived from those facts, so the UI only renders <see cref="Status"/>.
    /// Precedence: error, then connected, then connecting, then adapter state.
    /// </summary>
    public sealed class LinkStateMachine
    {
        private readonly object sync = new object();
        private List<AdapterInfo> adapters = new List<AdapterInfo>();
        private string peerName;        // a peer whose beacon is being heard
        private bool controlOpen;
        private string controlPeerName; // the peer of the open control session
        private LinkErrorKind errorKind = LinkErrorKind.None;
        private string errorMessage = string.Empty;
        private Permissions weAllow = Permissions.None;
        private Permissions peerAllows = Permissions.None;
        private LinkStatus current;

        /// <summary>Raised (outside any lock, on the reporting thread) whenever <see cref="Status"/> changes.</summary>
        public event EventHandler StateChanged;

        public LinkStateMachine()
        {
            current = Compute();
        }

        public LinkStatus Status
        {
            get { lock (sync) { return current; } }
        }

        public LinkState State
        {
            get { return Status.State; }
        }

        public void SetAdapters(IList<AdapterInfo> candidates)
        {
            Update(delegate { adapters = new List<AdapterInfo>(candidates); });
        }

        /// <summary>A peer beacon is being heard (null when it is gone).</summary>
        public void SetPeer(string name)
        {
            Update(delegate { peerName = name; });
        }

        public void SetControl(bool open, string name)
        {
            Update(delegate
            {
                controlOpen = open;
                controlPeerName = open ? name : null;
                if (!open)
                {
                    peerAllows = Permissions.None; // what the peer allowed belonged to that session
                }
            });
        }

        public void SetError(LinkErrorKind kind, string message)
        {
            Update(delegate { errorKind = kind; errorMessage = message; });
        }

        /// <summary>Clears the error if it is of the given kind.</summary>
        public void ClearError(LinkErrorKind kind)
        {
            Update(delegate
            {
                if (errorKind == kind)
                {
                    errorKind = LinkErrorKind.None;
                    errorMessage = string.Empty;
                }
            });
        }

        public void SetWeAllow(Permissions permissions)
        {
            Update(delegate { weAllow = permissions; });
        }

        public void SetPeerAllows(Permissions permissions)
        {
            Update(delegate { peerAllows = permissions; });
        }

        private delegate void Mutation();

        private void Update(Mutation mutate)
        {
            bool changed;
            lock (sync)
            {
                mutate();
                LinkStatus next = Compute();
                changed = !next.SameAs(current);
                current = next;
            }
            if (changed)
            {
                EventHandler handler = StateChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        private LinkStatus Compute()
        {
            LinkState state;
            string name = controlOpen ? controlPeerName : peerName;

            if (errorKind != LinkErrorKind.None)
            {
                state = LinkState.Error;
            }
            else if (controlOpen)
            {
                state = LinkState.Connected;
            }
            else if (peerName != null)
            {
                state = LinkState.Connecting;
            }
            else
            {
                bool usable = false;
                foreach (AdapterInfo adapter in adapters)
                {
                    if (adapter.IsUsable)
                    {
                        usable = true;
                    }
                }
                state = usable ? LinkState.Searching
                    : adapters.Count > 0 ? LinkState.WaitingForNetwork
                    : LinkState.NoCable;
            }

            return new LinkStatus(state, name == null ? string.Empty : name, errorKind, errorMessage, weAllow, peerAllows);
        }
    }
}
