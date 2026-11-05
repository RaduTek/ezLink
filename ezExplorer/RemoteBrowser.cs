using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using EzLink;
using EzLink.Protocol;

namespace ezExplorer
{
    internal delegate T Work<T>();

    /// <summary>Called on the UI thread; <paramref name="error"/> is set if the work threw.</summary>
    internal delegate void Completed<T>(T result, Exception error);

    internal enum ConnectResult
    {
        Connected,
        Denied,
        VersionMismatch
    }

    /// <summary>
    /// The explorer's connection to the peer: the two files sessions (one for browsing, one for transfers) and a
    /// worker thread that runs the blocking browse requests one after the other, so the UI never waits on the
    /// cable.
    /// </summary>
    internal sealed class RemoteBrowser : IDisposable, ITransferClientSource
    {
        private readonly ExplorerArgs args;
        private readonly Control ui;
        private readonly Queue<MethodInvoker> queue = new Queue<MethodInvoker>();
        private readonly object queueLock = new object();
        private readonly Thread worker;
        private readonly object clientLock = new object();
        private readonly object transferLock = new object();

        private IPAddress peerAddress;
        private IPAddress localAddress;
        private FileClient browse;
        private FileClient transfer;
        private Permissions granted = Permissions.None;
        private bool connected;
        private bool stopping;

        /// <summary>Raised on the UI thread when the connection comes up or goes down.</summary>
        public event EventHandler ConnectionChanged;

        public RemoteBrowser(ExplorerArgs args, Control ui)
        {
            this.args = args;
            this.ui = ui;
            peerAddress = args.PeerAddress;

            worker = new Thread(WorkerLoop);
            worker.Name = "ezExplorer browse";
            worker.IsBackground = true;
            worker.Start();
        }

        public string PeerName
        {
            get { return args.PeerName; }
        }

        public Guid PeerId
        {
            get { return args.PeerId; }
        }

        public bool Connected
        {
            get { lock (clientLock) { return connected; } }
        }

        /// <summary>What the peer lets this computer do.</summary>
        public Permissions Granted
        {
            get { lock (clientLock) { return granted; } }
        }

        /// <summary>The second files session, for the transfers of later milestones.</summary>
        public FileClient TransferClient
        {
            get { lock (clientLock) { return transfer; } }
        }

        /// <summary>
        /// The session for transfers, opened again if it was lost while the browsing session is still up. Runs
        /// blocking network code: call it from a worker thread.
        /// </summary>
        public FileClient GetTransferClient()
        {
            lock (transferLock)
            {
                FileClient current = TransferClient;
                if (current != null && !current.Session.IsClosed)
                {
                    return current;
                }
                if (!Connected || localAddress == null)
                {
                    throw new IOException(Strings.Get("ExplorerNotConnected"));
                }

                string version = Assembly.GetEntryAssembly().GetName().Version.ToString(3);
                HelloReplyMessage reply;
                FileClient fresh = FileClient.Connect(localAddress, new IPEndPoint(peerAddress, args.PeerPort),
                    args.SelfId, args.SelfName, version, out reply);
                if (fresh == null)
                {
                    throw new IOException(string.Format(Strings.Get("ExplorerDenied"), args.PeerName));
                }
                lock (clientLock)
                {
                    transfer = fresh;
                }
                return fresh;
            }
        }

        /// <summary>Notices that the link is gone after a transfer failed: the browsing session is closed too.</summary>
        public void CheckLink()
        {
            if (LinkLost())
            {
                DropSessions();
            }
        }

        public bool CanWrite
        {
            get { return Connected && RemotePaths.Allows(Granted, Permissions.Write); }
        }

        public bool CanRead
        {
            get { return Connected && RemotePaths.Allows(Granted, Permissions.Read); }
        }

        // ---- connecting ----

        /// <summary>Opens both files sessions. May wait for the peer's user to approve (up to two minutes).</summary>
        public void Connect(Completed<ConnectResult> done)
        {
            Run<ConnectResult>(new Work<ConnectResult>(DoConnect), done);
        }

        /// <summary>Drops the sessions and connects again, to a new address if the peer's changed.</summary>
        public void Reconnect(IPAddress newAddress, Completed<ConnectResult> done)
        {
            if (newAddress != null)
            {
                peerAddress = newAddress;
            }
            DropSessions();
            Connect(done);
        }

        private ConnectResult DoConnect()
        {
            IPAddress local = FindLocalAddress(peerAddress);
            if (local == null)
            {
                throw new IOException(string.Format(Strings.Get("ExplorerNoAdapter"), args.PeerName));
            }

            localAddress = local;
            string version = Assembly.GetEntryAssembly().GetName().Version.ToString(3);
            IPEndPoint endpoint = new IPEndPoint(peerAddress, args.PeerPort);

            HelloReplyMessage reply;
            FileClient first = FileClient.Connect(local, endpoint, args.SelfId, args.SelfName, version, out reply);
            if (first == null)
            {
                return reply.Status == HelloStatus.VersionMismatch ? ConnectResult.VersionMismatch : ConnectResult.Denied;
            }

            FileClient second;
            try
            {
                HelloReplyMessage reply2;
                second = FileClient.Connect(local, endpoint, args.SelfId, args.SelfName, version, out reply2);
                if (second == null)
                {
                    first.Close();
                    return ConnectResult.Denied;
                }
            }
            catch
            {
                first.Close();
                throw;
            }

            lock (clientLock)
            {
                browse = first;
                transfer = second;
                granted = reply.GrantedPermissions;
            }
            SetConnected(true);
            Log.Info("Connected to {0}; granted {1}", args.PeerName, reply.GrantedPermissions);
            return ConnectResult.Connected;
        }

        /// <summary>The local address on the same subnet as the peer, preferring the ezLink candidate adapters.</summary>
        private static IPAddress FindLocalAddress(IPAddress peer)
        {
            try
            {
                foreach (AdapterInfo adapter in AdapterLocator.FindCandidates())
                {
                    foreach (AdapterAddress address in adapter.UsableAddresses)
                    {
                        if (IpMath.IsInSubnet(peer, address.Address, address.Mask))
                        {
                            return address.Address;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Adapter detection failed: {0}", ex.Message);
            }

            // Not a candidate (for example the debug loopback peer): any interface on the peer's subnet will do.
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation address in nic.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        && IpMath.IsInSubnet(peer, address.Address, address.IPv4Mask))
                    {
                        return address.Address;
                    }
                }
            }
            return null;
        }

        private void DropSessions()
        {
            FileClient a;
            FileClient b;
            lock (clientLock)
            {
                a = browse;
                b = transfer;
                browse = null;
                transfer = null;
            }
            if (a != null)
            {
                a.Close();
            }
            if (b != null)
            {
                b.Close();
            }
            SetConnected(false);
        }

        /// <summary>Marks the link as lost (the peer or the cable went away).</summary>
        public void MarkDisconnected()
        {
            DropSessions();
        }

        private void SetConnected(bool value)
        {
            lock (clientLock)
            {
                if (connected == value)
                {
                    return;
                }
                connected = value;
            }
            Post(delegate
            {
                EventHandler handler = ConnectionChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            });
        }

        // ---- requests (all run on the worker thread) ----

        public void GetRoots(Completed<List<RemoteItem>> done)
        {
            Run<List<RemoteItem>>(delegate
            {
                return RemoteItem.FromDrives(Client().GetRoots());
            }, done);
        }

        public void ListDir(string path, Completed<List<RemoteItem>> done)
        {
            Run<List<RemoteItem>>(delegate
            {
                if (RemotePaths.IsRoot(path))
                {
                    return RemoteItem.FromDrives(Client().GetRoots());
                }
                return RemoteItem.FromEntries(path, Client().ListDir(path, false));
            }, done);
        }

        /// <summary>Subfolders only, for the tree.</summary>
        public void ListFolders(string path, Completed<List<RemoteItem>> done)
        {
            Run<List<RemoteItem>>(delegate
            {
                if (RemotePaths.IsRoot(path))
                {
                    return RemoteItem.FromDrives(Client().GetRoots());
                }
                return RemoteItem.FromEntries(path, Client().ListDir(path, true));
            }, done);
        }

        public void CreateDir(string path, Completed<bool> done)
        {
            Run<bool>(delegate { Client().CreateDir(path); return true; }, done);
        }

        public void Delete(string path, Completed<bool> done)
        {
            Run<bool>(delegate { Client().Delete(path); return true; }, done);
        }

        public void Rename(string oldPath, string newPath, Completed<bool> done)
        {
            Run<bool>(delegate { Client().Rename(oldPath, newPath); return true; }, done);
        }

        private FileClient Client()
        {
            lock (clientLock)
            {
                if (browse == null)
                {
                    throw new IOException(Strings.Get("ExplorerNotConnected"));
                }
                return browse;
            }
        }

        // ---- the worker ----

        /// <summary>Runs <paramref name="work"/> on the worker thread and reports back on the UI thread.</summary>
        public void Run<T>(Work<T> work, Completed<T> done)
        {
            Enqueue(delegate
            {
                T result = default(T);
                Exception error = null;
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    error = ex;
                    if (!(ex is RemoteException) && LinkLost())
                    {
                        Log.Warn("Link to {0} lost: {1}", args.PeerName, ex.Message);
                        DropSessions();
                    }
                }
                if (done != null)
                {
                    Post(delegate { done(result, error); });
                }
            });
        }

        private bool LinkLost()
        {
            FileClient client;
            lock (clientLock)
            {
                client = browse;
            }
            return client != null && client.Session.IsClosed;
        }

        private void Enqueue(MethodInvoker job)
        {
            lock (queueLock)
            {
                queue.Enqueue(job);
                Monitor.Pulse(queueLock);
            }
        }

        private void WorkerLoop()
        {
            while (true)
            {
                MethodInvoker job;
                lock (queueLock)
                {
                    while (queue.Count == 0 && !stopping)
                    {
                        Monitor.Wait(queueLock);
                    }
                    if (stopping)
                    {
                        return;
                    }
                    job = queue.Dequeue();
                }
                try
                {
                    job();
                }
                catch (Exception ex)
                {
                    Log.Error("Browse worker job failed", ex);
                }
            }
        }

        private void Post(MethodInvoker action)
        {
            try
            {
                if (ui.IsHandleCreated && !ui.IsDisposed)
                {
                    ui.BeginInvoke(action);
                }
            }
            catch (InvalidOperationException)
            {
                // The window is closing.
            }
        }

        public void Dispose()
        {
            lock (queueLock)
            {
                stopping = true;
                Monitor.PulseAll(queueLock);
            }
            DropSessions();
        }
    }
}
