using System;
using System.Net;
using System.Net.Sockets;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>Opens a session to a peer's listener.</summary>
    public static class SessionClient
    {
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(5);
        /// <summary>How long to wait for the user on the other side to answer an approval prompt.</summary>
        public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Connects from <paramref name="local"/> (the socket is bound to the link adapter's address) to the
        /// peer, sends <paramref name="hello"/> and waits for the verdict. Returns the session, not yet started,
        /// if the peer accepted; otherwise null with the peer's reply in <paramref name="reply"/>.
        /// Throws SocketException, TimeoutException, IOException or ProtocolException on failure.
        /// </summary>
        public static PeerSession Connect(IPAddress local, IPEndPoint peer, HelloMessage hello,
            out HelloReplyMessage reply)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SessionConnection connection = null;
            try
            {
                SessionConnection.ConfigureSocket(socket);
                socket.Bind(new IPEndPoint(local, 0));

                IAsyncResult pending = socket.BeginConnect(peer, null, null);
                if (!pending.AsyncWaitHandle.WaitOne(ConnectTimeout, false))
                {
                    throw new TimeoutException("Connecting to " + peer + " timed out");
                }
                socket.EndConnect(pending);

                connection = new SessionConnection(socket);
                uint requestId = 1;
                connection.Send(hello.ToFrame(requestId));

                reply = ReadReply(connection, ReplyTimeout);
                if (reply.Status == HelloStatus.PendingApproval)
                {
                    reply = ReadReply(connection, ApprovalTimeout);
                }
                if (reply.Status != HelloStatus.Accepted)
                {
                    connection.Close("Hello answered with " + reply.Status);
                    return null;
                }

                PeerSession session = new PeerSession(connection, hello.SessionType, true, hello.MachineId,
                    reply.MachineId, reply.ComputerName, reply.AppVersion, reply.Capabilities);
                session.GrantedToPeer = Permissions.None;
                return session;
            }
            catch
            {
                if (connection != null)
                {
                    connection.Close("connect failed");
                }
                else
                {
                    socket.Close();
                }
                throw;
            }
        }

        private static HelloReplyMessage ReadReply(SessionConnection connection, TimeSpan timeout)
        {
            Frame frame = connection.ReadFrame(timeout);
            if (frame == null)
            {
                throw new System.IO.IOException("Peer closed the connection during the handshake");
            }
            HelloReplyMessage reply = MessageFactory.Parse(frame) as HelloReplyMessage;
            if (reply == null)
            {
                throw new ProtocolException("Expected HelloReply, got " + frame.CommandSet + "/" + frame.Command);
            }
            return reply;
        }
    }
}
