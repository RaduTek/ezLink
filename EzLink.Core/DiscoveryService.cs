using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>Timing and port settings; the defaults are the plan's. Tests shorten the intervals.</summary>
    public sealed class DiscoveryOptions
    {
        public TimeSpan BeaconInterval = TimeSpan.FromSeconds(2);
        /// <summary>A peer is gone after this many missed beacon intervals (with no open session).</summary>
        public int MissedBeacons = 3;
        /// <summary>Fallback re-detection of adapters; XP sometimes misses change events.</summary>
        public TimeSpan DetectionInterval = TimeSpan.FromSeconds(5);
        public TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);
        /// <summary>UDP port the sockets bind to.</summary>
        public int LocalPort = Constants.DiscoveryPort;
        /// <summary>UDP port beacons are sent to.</summary>
        public int RemotePort = Constants.DiscoveryPort;

        public TimeSpan PeerTimeout
        {
            get { return TimeSpan.FromTicks(BeaconInterval.Ticks * MissedBeacons); }
        }
    }

    public sealed class PeerEventArgs : EventArgs
    {
        public readonly PeerInfo Peer;

        public PeerEventArgs(PeerInfo peer)
        {
            Peer = peer;
        }
    }

    public sealed class DuplicateMachineIdEventArgs : EventArgs
    {
        /// <summary>The address of the other machine that is using this machine's UUID.</summary>
        public readonly IPAddress From;

        public DuplicateMachineIdEventArgs(IPAddress from)
        {
            From = from;
        }
    }

    public sealed class ChannelErrorEventArgs : EventArgs
    {
        public readonly AdapterInfo Adapter;
        public readonly string Message;

        public ChannelErrorEventArgs(AdapterInfo adapter, string message)
        {
            Adapter = adapter;
            Message = message;
        }
    }

    /// <summary>
    /// Finds the other computer. Watches the candidate adapters (on network change events and on a timer),
    /// broadcasts a beacon on every candidate that is up and has an IPv4 address, listens for the peer's
    /// beacons and keeps the peer table. The adapter on which a peer answers is the ezLink link
    /// (<see cref="PeerInfo.AdapterId"/>).
    /// <para>Events are raised on background threads; handlers must be thread-safe or marshal to the UI.</para>
    /// </summary>
    public sealed class DiscoveryService : IDisposable
    {
        private static readonly TimeSpan DuplicateReportInterval = TimeSpan.FromSeconds(10);

        private readonly IAdapterSource source;
        private readonly Guid machineId;
        private readonly DiscoveryOptions options;
        private readonly byte[] beaconBytes;

        private readonly object sync = new object();
        private readonly Dictionary<string, BeaconChannel> channels = new Dictionary<string, BeaconChannel>();
        private readonly Dictionary<string, string> channelErrors = new Dictionary<string, string>();
        private readonly PeerTable table = new PeerTable();
        private List<AdapterInfo> candidates = new List<AdapterInfo>();
        private string candidateSignature = string.Empty;
        private DateTime lastDuplicateReportUtc = DateTime.MinValue;

        private Thread thread;
        private readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
        private volatile bool detectRequested;
        private bool started;

        public event EventHandler<PeerEventArgs> PeerFound;
        /// <summary>A known peer's address, port, name or adapter changed.</summary>
        public event EventHandler<PeerEventArgs> PeerChanged;
        public event EventHandler<PeerEventArgs> PeerLost;
        /// <summary>The candidate adapters, their status or their addresses changed; read <see cref="Candidates"/>.</summary>
        public event EventHandler AdaptersChanged;
        /// <summary>Another machine is using this machine's UUID (a cloned disk image).</summary>
        public event EventHandler<DuplicateMachineIdEventArgs> DuplicateMachineId;
        /// <summary>A beacon socket could not be opened (for example the port is in use).</summary>
        public event EventHandler<ChannelErrorEventArgs> ChannelError;

        public DiscoveryService(IAdapterSource source, Guid machineId, string computerName, string appVersion,
            ushort sessionPort, DiscoveryOptions options)
        {
            this.source = source;
            this.machineId = machineId;
            this.options = options != null ? options : new DiscoveryOptions();

            Beacon beacon = new Beacon();
            beacon.MachineId = machineId;
            beacon.TcpPort = sessionPort;
            beacon.ComputerName = computerName;
            beacon.AppVersion = appVersion;
            beaconBytes = beacon.ToBytes();
        }

        /// <summary>The candidate adapters as of the last detection.</summary>
        public List<AdapterInfo> Candidates
        {
            get
            {
                lock (sync)
                {
                    return new List<AdapterInfo>(candidates);
                }
            }
        }

        public List<PeerInfo> Peers
        {
            get
            {
                lock (sync)
                {
                    return table.ToList();
                }
            }
        }

        public void Start()
        {
            if (started)
            {
                return;
            }
            started = true;
            stopEvent.Reset();
            detectRequested = true;
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "ezLink discovery";
            thread.Start();
            Log.Info("Discovery started (UDP port {0}, beacon every {1} s)", options.LocalPort, options.BeaconInterval.TotalSeconds);
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }
            started = false;
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
            stopEvent.Set();
            if (thread != null)
            {
                thread.Join(3000);
                thread = null;
            }
            lock (sync)
            {
                foreach (BeaconChannel channel in channels.Values)
                {
                    channel.Close();
                }
                channels.Clear();
                table.Clear();
            }
            Log.Info("Discovery stopped");
        }

        public void Dispose()
        {
            Stop();
            stopEvent.Close();
        }

        /// <summary>Asks the worker to re-detect adapters now instead of waiting for the next tick.</summary>
        public void RefreshAdapters()
        {
            detectRequested = true;
        }

        /// <summary>Tells discovery a session with the peer is open (it is then not expired for missing beacons) or closed.</summary>
        public void SetSessionOpen(Guid peerId, bool open)
        {
            lock (sync)
            {
                table.SetSessionOpen(peerId, open);
            }
        }

        private void OnNetworkChanged(object sender, EventArgs e)
        {
            detectRequested = true;
        }

        private void OnAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            detectRequested = true;
        }

        private void Run()
        {
            DateTime nextDetect = DateTime.UtcNow;
            DateTime nextBeacon = DateTime.UtcNow;
            while (!stopEvent.WaitOne(options.TickInterval, false))
            {
                try
                {
                    DateTime now = DateTime.UtcNow;
                    if (detectRequested || now >= nextDetect)
                    {
                        detectRequested = false;
                        DetectAdapters();
                        nextDetect = now + options.DetectionInterval;
                    }
                    if (now >= nextBeacon)
                    {
                        SendBeacons();
                        nextBeacon = now + options.BeaconInterval;
                    }
                    ExpirePeers(now);
                }
                catch (Exception ex)
                {
                    Log.Error("Discovery loop error", ex);
                }
            }
        }

        private void DetectAdapters()
        {
            List<AdapterInfo> found;
            try
            {
                found = source.GetCandidates();
            }
            catch (Exception ex)
            {
                Log.Error("Adapter detection failed", ex);
                return;
            }

            List<PeerInfo> lost = new List<PeerInfo>();
            List<ChannelErrorEventArgs> errors = new List<ChannelErrorEventArgs>();
            bool changed;

            lock (sync)
            {
                string signature = Signature(found);
                changed = signature != candidateSignature;
                if (changed)
                {
                    LogAdapters(found);
                }
                candidateSignature = signature;
                candidates = found;

                // Which (adapter, address) pairs should have a channel: up, with a real IPv4 address.
                Dictionary<string, KeyValuePair<AdapterInfo, AdapterAddress>> wanted =
                    new Dictionary<string, KeyValuePair<AdapterInfo, AdapterAddress>>();
                foreach (AdapterInfo adapter in found)
                {
                    foreach (AdapterAddress address in adapter.UsableAddresses)
                    {
                        wanted[BeaconChannel.MakeKey(adapter, address)] =
                            new KeyValuePair<AdapterInfo, AdapterAddress>(adapter, address);
                    }
                }

                // Close channels that are no longer wanted (or died); their peers are gone with them.
                List<string> toClose = new List<string>();
                foreach (KeyValuePair<string, BeaconChannel> pair in channels)
                {
                    if (!wanted.ContainsKey(pair.Key) || pair.Value.Faulted)
                    {
                        toClose.Add(pair.Key);
                    }
                }
                foreach (string key in toClose)
                {
                    BeaconChannel channel = channels[key];
                    channels.Remove(key);
                    channel.Close();
                    Log.Info("Closed beacon channel {0}", channel);
                    lost.AddRange(table.RemoveByChannel(channel.Adapter.Id, channel.Local.Address));
                    changed = true;
                }

                // Open channels for new pairs. A failed bind is retried on the next detection.
                foreach (KeyValuePair<string, KeyValuePair<AdapterInfo, AdapterAddress>> pair in wanted)
                {
                    if (channels.ContainsKey(pair.Key))
                    {
                        continue;
                    }
                    BeaconChannel channel = new BeaconChannel(pair.Value.Key, pair.Value.Value,
                        options.LocalPort, options.RemotePort, OnBeacon);
                    try
                    {
                        channel.Open();
                        channels[pair.Key] = channel;
                        channelErrors.Remove(pair.Key);
                        Log.Info("Opened beacon channel {0}", channel);
                        channel.Send(beaconBytes);
                        changed = true;
                    }
                    catch (SocketException ex)
                    {
                        string message = ex.Message + " (" + ex.ErrorCode + ")";
                        string previous;
                        if (!channelErrors.TryGetValue(pair.Key, out previous) || previous != message)
                        {
                            channelErrors[pair.Key] = message;
                            Log.Error("Cannot open beacon channel {0}: {1}", channel, message);
                            errors.Add(new ChannelErrorEventArgs(pair.Value.Key, message));
                        }
                    }
                }
            }

            foreach (PeerInfo peer in lost)
            {
                Log.Info("Peer lost (adapter gone): {0}", peer);
                Raise(PeerLost, new PeerEventArgs(peer));
            }
            foreach (ChannelErrorEventArgs error in errors)
            {
                EventHandler<ChannelErrorEventArgs> handler = ChannelError;
                if (handler != null)
                {
                    handler(this, error);
                }
            }
            if (changed)
            {
                EventHandler handler = AdaptersChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        private void SendBeacons()
        {
            bool failed = false;
            lock (sync)
            {
                foreach (BeaconChannel channel in channels.Values)
                {
                    if (!channel.Send(beaconBytes))
                    {
                        failed = true;
                    }
                }
            }
            if (failed)
            {
                detectRequested = true; // probably unplugged: look again right away
            }
        }

        private void ExpirePeers(DateTime now)
        {
            List<PeerInfo> gone;
            lock (sync)
            {
                gone = table.Expire(now, options.PeerTimeout);
            }
            foreach (PeerInfo peer in gone)
            {
                Log.Info("Peer lost (no beacon for {0} s): {1}", options.PeerTimeout.TotalSeconds, peer);
                Raise(PeerLost, new PeerEventArgs(peer));
            }
        }

        private void OnBeacon(BeaconChannel channel, Beacon beacon, IPEndPoint from)
        {
            bool isLocal = source.IsLocalEndpoint(from);

            if (beacon.MachineId == machineId)
            {
                // Our own broadcast coming back, or another adapter of this machine, is normal.
                // The same UUID from somewhere else means two machines share one UUID.
                if (!isLocal)
                {
                    ReportDuplicate(from.Address);
                }
                return;
            }
            if (isLocal)
            {
                return;
            }

            PeerInfo snapshot;
            PeerUpdate update;
            lock (sync)
            {
                if (!channels.ContainsKey(channel.Key))
                {
                    return; // the channel was closed while this beacon was in flight
                }
                update = table.Update(beacon, from.Address, channel.Adapter.Id, channel.Local.Address,
                    DateTime.UtcNow, out snapshot);
            }

            if (update == PeerUpdate.Added)
            {
                Log.Info("Peer found: {0} on {1} via {2}", snapshot, channel.Adapter.Name, channel.Local.Address);
                channel.Send(beaconBytes); // answer at once so the peer sees us without waiting for our next beacon
                Raise(PeerFound, new PeerEventArgs(snapshot));
            }
            else if (update == PeerUpdate.Changed)
            {
                Log.Info("Peer changed: {0} on {1} via {2}", snapshot, channel.Adapter.Name, channel.Local.Address);
                Raise(PeerChanged, new PeerEventArgs(snapshot));
            }
        }

        private void ReportDuplicate(IPAddress from)
        {
            DateTime now = DateTime.UtcNow;
            lock (sync)
            {
                if (now - lastDuplicateReportUtc < DuplicateReportInterval)
                {
                    return;
                }
                lastDuplicateReportUtc = now;
            }
            Log.Error("Another computer ({0}) is using this computer's UUID {1}", from, machineId);
            EventHandler<DuplicateMachineIdEventArgs> handler = DuplicateMachineId;
            if (handler != null)
            {
                handler(this, new DuplicateMachineIdEventArgs(from));
            }
        }

        private void Raise(EventHandler<PeerEventArgs> handler, PeerEventArgs args)
        {
            if (handler != null)
            {
                handler(this, args);
            }
        }

        private static string Signature(List<AdapterInfo> adapters)
        {
            List<string> parts = new List<string>();
            foreach (AdapterInfo adapter in adapters)
            {
                parts.Add(adapter.Signature);
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("\n", parts.ToArray());
        }

        private static void LogAdapters(List<AdapterInfo> adapters)
        {
            if (adapters.Count == 0)
            {
                Log.Info("No ezLink candidate adapter present");
                return;
            }
            foreach (AdapterInfo adapter in adapters)
            {
                Log.Info("Candidate adapter: {0}", adapter);
            }
        }
    }
}
