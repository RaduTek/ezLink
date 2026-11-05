using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    public sealed class FrameEventArgs : EventArgs
    {
        public readonly Frame Frame;

        public FrameEventArgs(Frame frame)
        {
            Frame = frame;
        }
    }

    public sealed class ConnectionClosedEventArgs : EventArgs
    {
        public readonly string Reason;

        public ConnectionClosedEventArgs(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// A TCP connection carrying protocol frames. The handshake is done synchronously with
    /// <see cref="ReadFrame"/> and <see cref="Send"/>; <see cref="StartReading"/> then switches to a
    /// background read thread that raises <see cref="FrameReceived"/> until the connection closes.
    /// </summary>
    public sealed class SessionConnection
    {
        /// <summary>XP has no TCP window auto-tuning, so the buffers are set explicitly.</summary>
        public const int SocketBufferSize = 256 * 1024;

        private readonly Socket socket;
        private readonly NetworkStream stream;
        private readonly IPEndPoint localEndpoint;
        private readonly IPEndPoint remoteEndpoint;
        private readonly object sendLock = new object();
        private int closed;

        /// <summary>Raised on the read thread for every frame received.</summary>
        public event EventHandler<FrameEventArgs> FrameReceived;
        /// <summary>Raised once, on whichever thread closes the connection.</summary>
        public event EventHandler<ConnectionClosedEventArgs> Closed;

        public SessionConnection(Socket socket)
        {
            this.socket = socket;
            localEndpoint = (IPEndPoint)socket.LocalEndPoint;
            remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
            ConfigureSocket(socket);
            stream = new NetworkStream(socket, false);
        }

        public static void ConfigureSocket(Socket socket)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, SocketBufferSize);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, SocketBufferSize);
            socket.NoDelay = true; // small ping/request frames must not wait for Nagle
        }

        public IPEndPoint LocalEndPoint
        {
            get { return localEndpoint; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return remoteEndpoint; }
        }

        public bool IsClosed
        {
            get { return Thread.VolatileRead(ref closed) != 0; }
        }

        /// <summary>
        /// Reads one frame, waiting at most <paramref name="timeout"/>. Returns null if the peer closed the
        /// connection. Throws <see cref="TimeoutException"/> or <see cref="ProtocolException"/>.
        /// For the handshake, before <see cref="StartReading"/>.
        /// </summary>
        public Frame ReadFrame(TimeSpan timeout)
        {
            socket.ReceiveTimeout = (int)timeout.TotalMilliseconds;
            try
            {
                return FrameStream.Read(stream);
            }
            catch (IOException ex)
            {
                SocketException inner = ex.InnerException as SocketException;
                if (inner != null && inner.SocketErrorCode == SocketError.TimedOut)
                {
                    throw new TimeoutException("No data from " + remoteEndpoint + " within " + timeout.TotalSeconds + " s");
                }
                throw;
            }
            finally
            {
                if (!IsClosed)
                {
                    socket.ReceiveTimeout = 0;
                }
            }
        }

        /// <summary>Sends a frame. Closes the connection and throws IOException if the write fails.</summary>
        public void Send(Frame frame)
        {
            try
            {
                lock (sendLock)
                {
                    FrameStream.Write(stream, frame);
                }
            }
            catch (IOException ex)
            {
                Close("send failed: " + ex.Message);
                throw;
            }
            catch (ObjectDisposedException)
            {
                throw new IOException("Connection is closed");
            }
        }

        public void StartReading()
        {
            Thread thread = new Thread(ReadLoop);
            thread.IsBackground = true;
            thread.Name = "ezLink session " + remoteEndpoint;
            thread.Start();
        }

        public void Close(string reason)
        {
            if (Interlocked.Exchange(ref closed, 1) != 0)
            {
                return;
            }
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            socket.Close();

            EventHandler<ConnectionClosedEventArgs> handler = Closed;
            if (handler != null)
            {
                handler(this, new ConnectionClosedEventArgs(reason));
            }
        }

        private void ReadLoop()
        {
            string reason = "connection closed by peer";
            try
            {
                while (!IsClosed)
                {
                    Frame frame = FrameStream.Read(stream);
                    if (frame == null)
                    {
                        break;
                    }
                    EventHandler<FrameEventArgs> handler = FrameReceived;
                    if (handler != null)
                    {
                        try
                        {
                            handler(this, new FrameEventArgs(frame));
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Frame handler threw (set " + frame.CommandSet + ", command " + frame.Command + ")", ex);
                        }
                    }
                }
            }
            catch (ProtocolException ex)
            {
                reason = "protocol error: " + ex.Message;
            }
            catch (IOException ex)
            {
                reason = "connection error: " + ex.Message;
            }
            catch (ObjectDisposedException)
            {
                reason = "closed";
            }
            Close(reason);
        }
    }
}
