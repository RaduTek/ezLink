using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>
    /// A UDP socket bound to one adapter address (never to "any"): sends beacons to that subnet's broadcast
    /// address and receives the peer's beacons on its own thread.
    /// </summary>
    internal sealed class BeaconChannel
    {
        private const int WsaConnReset = 10054;

        public delegate void BeaconHandler(BeaconChannel channel, Beacon beacon, IPEndPoint from);

        private readonly AdapterInfo adapter;
        private readonly AdapterAddress local;
        private readonly int localPort;
        private readonly IPEndPoint broadcastEndpoint;
        private readonly BeaconHandler handler;
        private Socket socket;
        private Thread thread;
        private volatile bool closed;
        private volatile bool faulted;

        public BeaconChannel(AdapterInfo adapter, AdapterAddress local, int localPort, int remotePort, BeaconHandler handler)
        {
            this.adapter = adapter;
            this.local = local;
            this.localPort = localPort;
            this.handler = handler;
            broadcastEndpoint = new IPEndPoint(local.BroadcastAddress, remotePort);
        }

        public AdapterInfo Adapter
        {
            get { return adapter; }
        }

        public AdapterAddress Local
        {
            get { return local; }
        }

        public string Key
        {
            get { return MakeKey(adapter, local); }
        }

        /// <summary>True if the receive thread died on an unexpected socket error; the service reopens the channel.</summary>
        public bool Faulted
        {
            get { return faulted; }
        }

        public static string MakeKey(AdapterInfo adapter, AdapterAddress address)
        {
            return adapter.Id + "|" + address.Address;
        }

        public override string ToString()
        {
            return adapter.Name + " " + local.Address + ":" + localPort + " -> " + broadcastEndpoint;
        }

        /// <summary>Binds the socket and starts receiving. Throws SocketException if the address or port is unavailable.</summary>
        public void Open()
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                s.Bind(new IPEndPoint(local.Address, localPort));
            }
            catch
            {
                s.Close();
                throw;
            }
            socket = s;
            thread = new Thread(ReceiveLoop);
            thread.IsBackground = true;
            thread.Name = "ezLink beacon " + local.Address;
            thread.Start();
        }

        /// <summary>Sends a beacon to the subnet broadcast address. Returns false if the send failed.</summary>
        public bool Send(byte[] beacon)
        {
            try
            {
                socket.SendTo(beacon, broadcastEndpoint);
                return true;
            }
            catch (SocketException ex)
            {
                Log.Warn("Beacon send on {0} failed: {1}", this, ex.Message);
            }
            catch (ObjectDisposedException)
            {
            }
            return false;
        }

        public void Close()
        {
            closed = true;
            Socket s = socket;
            if (s != null)
            {
                try
                {
                    s.Close();
                }
                catch (SocketException)
                {
                }
            }
            Thread t = thread;
            if (t != null && t != Thread.CurrentThread)
            {
                t.Join(1000);
            }
        }

        private void ReceiveLoop()
        {
            byte[] buffer = new byte[2048];
            while (!closed)
            {
                try
                {
                    EndPoint remote = new IPEndPoint(IPAddress.Any, 0); // only a placeholder for the sender
                    int length = socket.ReceiveFrom(buffer, ref remote);
                    byte[] datagram = new byte[length];
                    Buffer.BlockCopy(buffer, 0, datagram, 0, length);

                    Beacon beacon;
                    try
                    {
                        beacon = Beacon.Parse(datagram);
                    }
                    catch (ProtocolException)
                    {
                        continue; // not ours, or malformed: ignore silently
                    }
                    handler(this, beacon, (IPEndPoint)remote);
                }
                catch (SocketException ex)
                {
                    if (closed)
                    {
                        break;
                    }
                    if (ex.ErrorCode == WsaConnReset)
                    {
                        continue; // ICMP "port unreachable" from an earlier send; harmless on UDP
                    }
                    Log.Warn("Beacon receive on {0} failed: {1}", this, ex.Message);
                    faulted = true;
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error("Beacon handler on " + this + " threw", ex);
                }
            }
        }
    }
}
