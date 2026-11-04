using System;
using System.Collections.Generic;
using System.Net;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>A peer learned from its beacon.</summary>
    public sealed class PeerInfo
    {
        public Guid MachineId;
        public string ComputerName = string.Empty;
        public string AppVersion = string.Empty;
        /// <summary>The peer's address (the beacon's source).</summary>
        public IPAddress Address;
        public ushort TcpPort;
        /// <summary>The adapter and local address the beacon arrived on; that adapter is the ezLink link.</summary>
        public string AdapterId = string.Empty;
        public IPAddress LocalAddress;
        public DateTime FirstSeenUtc;
        public DateTime LastSeenUtc;
        /// <summary>While a session is open the peer is not expired for missing beacons.</summary>
        public bool SessionOpen;

        public PeerInfo Clone()
        {
            return (PeerInfo)MemberwiseClone();
        }

        public override string ToString()
        {
            return ComputerName + " (" + MachineId + ") at " + Address + ":" + TcpPort;
        }
    }

    public enum PeerUpdate
    {
        /// <summary>Known peer, nothing but the timestamp changed.</summary>
        Refreshed,
        Added,
        /// <summary>Known peer whose address, port, name or adapter changed.</summary>
        Changed
    }

    /// <summary>
    /// The set of peers currently present. Not thread-safe: <see cref="DiscoveryService"/> serializes access.
    /// Time is passed in so expiry can be tested without waiting.
    /// </summary>
    public sealed class PeerTable
    {
        private readonly Dictionary<Guid, PeerInfo> peers = new Dictionary<Guid, PeerInfo>();

        public int Count
        {
            get { return peers.Count; }
        }

        /// <summary>Records a beacon. <paramref name="snapshot"/> is a copy of the peer's state afterwards.</summary>
        public PeerUpdate Update(Beacon beacon, IPAddress source, string adapterId, IPAddress localAddress,
            DateTime nowUtc, out PeerInfo snapshot)
        {
            PeerInfo peer;
            PeerUpdate result;
            if (!peers.TryGetValue(beacon.MachineId, out peer))
            {
                peer = new PeerInfo();
                peer.MachineId = beacon.MachineId;
                peer.FirstSeenUtc = nowUtc;
                peers[beacon.MachineId] = peer;
                result = PeerUpdate.Added;
            }
            else
            {
                bool changed = !peer.Address.Equals(source)
                    || peer.TcpPort != beacon.TcpPort
                    || peer.ComputerName != beacon.ComputerName
                    || peer.AppVersion != beacon.AppVersion
                    || peer.AdapterId != adapterId
                    || !peer.LocalAddress.Equals(localAddress);
                result = changed ? PeerUpdate.Changed : PeerUpdate.Refreshed;
            }

            peer.Address = source;
            peer.TcpPort = beacon.TcpPort;
            peer.ComputerName = beacon.ComputerName;
            peer.AppVersion = beacon.AppVersion;
            peer.AdapterId = adapterId;
            peer.LocalAddress = localAddress;
            peer.LastSeenUtc = nowUtc;
            snapshot = peer.Clone();
            return result;
        }

        /// <summary>Removes and returns peers not heard from for longer than the timeout and without an open session.</summary>
        public List<PeerInfo> Expire(DateTime nowUtc, TimeSpan timeout)
        {
            List<PeerInfo> gone = new List<PeerInfo>();
            foreach (PeerInfo peer in peers.Values)
            {
                if (!peer.SessionOpen && nowUtc - peer.LastSeenUtc > timeout)
                {
                    gone.Add(peer.Clone());
                }
            }
            foreach (PeerInfo peer in gone)
            {
                peers.Remove(peer.MachineId);
            }
            return gone;
        }

        /// <summary>Removes every peer that was heard on the given adapter and local address (the channel was closed).</summary>
        public List<PeerInfo> RemoveByChannel(string adapterId, IPAddress localAddress)
        {
            List<PeerInfo> gone = new List<PeerInfo>();
            foreach (PeerInfo peer in peers.Values)
            {
                if (peer.AdapterId == adapterId && peer.LocalAddress.Equals(localAddress))
                {
                    gone.Add(peer.Clone());
                }
            }
            foreach (PeerInfo peer in gone)
            {
                peers.Remove(peer.MachineId);
            }
            return gone;
        }

        /// <summary>Marks a peer as having (or no longer having) an open session. Returns false for an unknown peer.</summary>
        public bool SetSessionOpen(Guid machineId, bool open)
        {
            PeerInfo peer;
            if (!peers.TryGetValue(machineId, out peer))
            {
                return false;
            }
            peer.SessionOpen = open;
            return true;
        }

        public PeerInfo Get(Guid machineId)
        {
            PeerInfo peer;
            return peers.TryGetValue(machineId, out peer) ? peer.Clone() : null;
        }

        public List<PeerInfo> ToList()
        {
            List<PeerInfo> list = new List<PeerInfo>();
            foreach (PeerInfo peer in peers.Values)
            {
                list.Add(peer.Clone());
            }
            return list;
        }

        public void Clear()
        {
            peers.Clear();
        }
    }
}
