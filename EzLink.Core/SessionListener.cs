using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>The server-side policy of a listener: who may open which kind of session.</summary>
    public interface ISessionHost
    {
        /// <summary>
        /// Decides the reply to a Hello. May block (for example while the user decides); it may send an interim
        /// PendingApproval reply on <paramref name="connection"/> itself before returning the final one.
        /// </summary>
        HelloReplyMessage HandleHello(HelloMessage hello, IPEndPoint remote, SessionConnection connection);

        /// <summary>
        /// Called for an accepted session before it starts receiving: register command sets here.
        /// Return false to drop the session.
        /// </summary>
        bool SessionEstablished(PeerSession session);
    }

    /// <summary>
    /// Accepts session connections on one adapter address. It binds to that address only (never "any") and
    /// additionally rejects every connection whose local endpoint is not that address or whose remote address
    /// is outside the adapter's subnet, because on XP's weak host model a packet for this address can arrive
    /// through another interface.
    /// </summary>
    public sealed class SessionListener
    {
        private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(5);

        private readonly IPAddress address;
        private readonly IPAddress mask;
        private readonly int port;
        private readonly Guid localId;
        private readonly ISessionHost host;
        private Socket listener;
        private Thread thread;
        private volatile bool stopped;

        public SessionListener(IPAddress address, IPAddress mask, int port, Guid localId, ISessionHost host)
        {
            this.address = address;
            this.mask = mask;
            this.port = port;
            this.localId = localId;
            this.host = host;
        }

        public IPAddress Address
        {
            get { return address; }
        }

        public override string ToString()
        {
            return address + ":" + port;
        }

        /// <summary>Binds and starts accepting. Throws SocketException, for example if the port is in use.</summary>
        public void Start()
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                // No ReuseAddress here: a second listener on the port must fail visibly.
                SessionConnection.ConfigureSocket(s);
                s.Bind(new IPEndPoint(address, port));
                s.Listen(8);
            }
            catch
            {
                s.Close();
                throw;
            }
            listener = s;
            thread = new Thread(AcceptLoop);
            thread.IsBackground = true;
            thread.Name = "ezLink listener " + this;
            thread.Start();
        }

        public void Stop()
        {
            stopped = true;
            Socket s = listener;
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

        private void AcceptLoop()
        {
            while (!stopped)
            {
                Socket client;
                try
                {
                    client = listener.Accept();
                }
                catch (SocketException)
                {
                    if (stopped)
                    {
                        return;
                    }
                    Thread.Sleep(100);
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                // A dedicated thread: handshakes can block for seconds and must not wait behind a starved thread pool.
                Thread handler = new Thread(HandleClient);
                handler.IsBackground = true;
                handler.Name = "ezLink handshake " + client.RemoteEndPoint;
                handler.Start(client);
            }
        }

        private void HandleClient(object state)
        {
            Socket client = (Socket)state;
            SessionConnection connection = null;
            try
            {
                IPEndPoint local = (IPEndPoint)client.LocalEndPoint;
                IPEndPoint remote = (IPEndPoint)client.RemoteEndPoint;
                if (!local.Address.Equals(address) || !IpMath.IsInSubnet(remote.Address, address, mask))
                {
                    Log.Warn("Rejected connection from {0} to {1}: not on the link ({2}/{3})", remote, local, address, mask);
                    client.Close();
                    return;
                }

                connection = new SessionConnection(client);
                Frame first = connection.ReadFrame(HelloTimeout);
                if (first == null)
                {
                    connection.Close("closed before Hello");
                    return;
                }
                if (first.CommandSet != CommandSets.General || first.Command != GeneralCommands.Hello)
                {
                    Log.Warn("First frame from {0} was not Hello; closing", remote);
                    connection.Close("first message was not Hello");
                    return;
                }

                HelloMessage hello = (HelloMessage)MessageFactory.Parse(first);
                HelloReplyMessage reply = host.HandleHello(hello, remote, connection);
                connection.Send(reply.ToFrame(first.RequestId));
                if (reply.Status != HelloStatus.Accepted)
                {
                    connection.Close("Hello answered with " + reply.Status);
                    return;
                }

                PeerSession session = new PeerSession(connection, hello.SessionType, false, localId,
                    hello.MachineId, hello.ComputerName, hello.AppVersion, hello.Capabilities);
                session.GrantedToPeer = reply.GrantedPermissions;
                if (!host.SessionEstablished(session))
                {
                    session.Close("dropped by host");
                    return;
                }
                session.Start();
            }
            catch (Exception ex)
            {
                // Timeouts, malformed Hello, connection resets: none of them may take the listener down.
                Log.Warn("Incoming connection failed: {0}", ex.Message);
                if (connection != null)
                {
                    connection.Close("handshake failed");
                }
                else
                {
                    client.Close();
                }
            }
        }
    }
}
