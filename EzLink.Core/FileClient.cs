using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>The peer answered a request with an Error, or the transfer failed its checksum.</summary>
    public sealed class RemoteException : IOException
    {
        public readonly ErrorCode Code;

        public RemoteException(ErrorCode code, string message)
            : base(message)
        {
            Code = code;
        }
    }

    /// <summary>Called with the number of bytes of the current file transferred so far.</summary>
    public delegate void TransferProgress(long bytesDone);

    /// <summary>
    /// The file-transfer command set (files/1), client side: one instance per files session opened to a peer.
    /// Calls are blocking and one at a time (the server also handles one request at a time per session);
    /// <see cref="Cancel"/> may be called from another thread to stop the current transfer.
    /// <para>
    /// Register by constructing it on a session that has not been started yet, then start the session.
    /// <see cref="Connect"/> does both.
    /// </para>
    /// </summary>
    public sealed class FileClient : ICommandSet
    {
        private readonly PeerSession session;
        private readonly object callLock = new object();   // one request at a time
        private readonly object sync = new object();        // guards the reply queue
        private readonly Queue<Message> replies = new Queue<Message>();
        private uint currentId;
        private bool closed;
        private string closedReason = string.Empty;

        /// <summary>How long to wait for the next reply frame before giving up.</summary>
        public TimeSpan ReplyTimeout = TimeSpan.FromSeconds(30);

        public FileClient(PeerSession session)
        {
            this.session = session;
            session.Register(this);
            session.ReplyReceived += OnGeneralReply;
            session.Closed += OnClosed;
        }

        public byte CommandSet
        {
            get { return CommandSets.Files; }
        }

        public string Capability
        {
            get { return Capabilities.Files; }
        }

        public PeerSession Session
        {
            get { return session; }
        }

        /// <summary>
        /// Opens a files session to the peer and starts it. Returns null if the peer refused, with its reply in
        /// <paramref name="reply"/> (status Denied, or VersionMismatch). Throws on network failure.
        /// </summary>
        public static FileClient Connect(IPAddress local, IPEndPoint peer, Guid machineId, string computerName,
            string appVersion, out HelloReplyMessage reply)
        {
            HelloMessage hello = new HelloMessage();
            hello.SessionType = SessionTypes.Files;
            hello.MachineId = machineId;
            hello.ComputerName = computerName;
            hello.AppVersion = appVersion;
            hello.Capabilities.Add(Capabilities.Files);

            PeerSession session = SessionClient.Connect(local, peer, hello, out reply);
            if (session == null)
            {
                return null;
            }
            if (!session.PeerCapabilities.Contains(Capabilities.Files))
            {
                session.Close("the peer does not offer file access");
                throw new RemoteException(ErrorCode.IoError, Strings.Get("ErrorFilesNotOffered"));
            }
            FileClient client = new FileClient(session);
            session.Start();
            return client;
        }

        public void Close()
        {
            session.Close("closed by the user");
        }

        // ---- receiving ----

        public void Handle(Frame frame, PeerSession from)
        {
            Message message;
            try
            {
                message = MessageFactory.Parse(frame);
            }
            catch (ProtocolException ex)
            {
                from.Close("protocol error: " + ex.Message);
                return;
            }
            if (message != null)
            {
                Deliver(message, frame.RequestId);
            }
        }

        private void OnGeneralReply(object sender, MessageEventArgs e)
        {
            Deliver(e.Message, e.RequestId);
        }

        private void Deliver(Message message, uint requestId)
        {
            lock (sync)
            {
                if (requestId != currentId || currentId == 0)
                {
                    return; // a late frame of a request that is already over
                }
                replies.Enqueue(message);
                Monitor.PulseAll(sync);
            }
        }

        private void OnClosed(object sender, SessionClosedEventArgs e)
        {
            lock (sync)
            {
                closed = true;
                closedReason = e.Reason;
                Monitor.PulseAll(sync);
            }
        }

        /// <summary>Waits for the next reply to the current request. Throws if the connection is lost or nothing arrives.</summary>
        private Message Next()
        {
            DateTime until = DateTime.UtcNow + ReplyTimeout;
            lock (sync)
            {
                while (replies.Count == 0)
                {
                    if (closed)
                    {
                        throw new RemoteException(ErrorCode.IoError, Strings.Get("ErrorConnectionLost"));
                    }
                    TimeSpan left = until - DateTime.UtcNow;
                    if (left <= TimeSpan.Zero)
                    {
                        throw new RemoteException(ErrorCode.IoError, Strings.Get("ErrorRequestTimeout"));
                    }
                    Monitor.Wait(sync, left);
                }
                return replies.Dequeue();
            }
        }

        /// <summary>Takes a reply that is already there, without waiting. Null if none.</summary>
        private Message Poll()
        {
            lock (sync)
            {
                return replies.Count > 0 ? replies.Dequeue() : null;
            }
        }

        private static Exception ToException(Message reply)
        {
            ErrorMessage error = reply as ErrorMessage;
            if (error != null)
            {
                return new RemoteException(error.Code, error.Text);
            }
            return new RemoteException(ErrorCode.IoError, "Unexpected reply " + reply.CommandSet + "/" + reply.Command);
        }

        private uint Begin()
        {
            uint id = session.NextRequestId();
            lock (sync)
            {
                if (closed)
                {
                    throw new RemoteException(ErrorCode.IoError, Strings.Get("ErrorConnectionLost"));
                }
                replies.Clear();
                currentId = id;
            }
            return id;
        }

        private void End()
        {
            lock (sync)
            {
                currentId = 0;
                replies.Clear();
            }
        }

        private void Send(Message message, uint id)
        {
            if (!session.Send(message, id))
            {
                throw new RemoteException(ErrorCode.IoError, Strings.Get("ErrorConnectionLost"));
            }
        }

        /// <summary>Asks the peer to stop the request that is running now (a transfer, or a long listing).</summary>
        public void Cancel()
        {
            uint id;
            lock (sync)
            {
                id = currentId;
            }
            if (id != 0)
            {
                session.Send(new CancelMessage(id), 0);
            }
        }

        // ---- requests ----

        public List<DriveEntry> GetRoots()
        {
            lock (callLock)
            {
                uint id = Begin();
                try
                {
                    Send(new GetRootsRequest(), id);
                    return Expect<RootsReply>(Next()).Drives;
                }
                finally
                {
                    End();
                }
            }
        }

        private T Expect<T>(Message m) where T : Message
        {
            T typed = m as T;
            if (typed == null)
            {
                throw ToException(m);
            }
            return typed;
        }

        public List<FileEntry> ListDir(string path, bool foldersOnly)
        {
            lock (callLock)
            {
                uint id = Begin();
                try
                {
                    Send(new ListDirRequest(path, foldersOnly), id);
                    List<FileEntry> all = new List<FileEntry>();
                    while (true)
                    {
                        DirListingReply part = Expect<DirListingReply>(Next());
                        all.AddRange(part.Entries);
                        if (part.EndOfListing)
                        {
                            return all;
                        }
                    }
                }
                finally
                {
                    End();
                }
            }
        }

        /// <summary>Recursive listing of a folder; names are paths relative to it, parents before their children.</summary>
        public List<FileEntry> ListTree(string path)
        {
            lock (callLock)
            {
                uint id = Begin();
                try
                {
                    Send(new ListTreeRequest(path), id);
                    List<FileEntry> all = new List<FileEntry>();
                    while (true)
                    {
                        Message m = Next();
                        if (m is TreeEndReply)
                        {
                            return all;
                        }
                        all.AddRange(Expect<TreeEntriesReply>(m).Entries);
                    }
                }
                finally
                {
                    End();
                }
            }
        }

        public StatResultReply Stat(string path)
        {
            lock (callLock)
            {
                uint id = Begin();
                try
                {
                    Send(new StatRequest(path), id);
                    return Expect<StatResultReply>(Next());
                }
                finally
                {
                    End();
                }
            }
        }

        /// <summary>
        /// Downloads the file from <paramref name="offset"/> into <paramref name="destination"/> and checks the
        /// SHA-256 the server computed over the bytes it sent. Returns that hash.
        /// </summary>
        public byte[] ReadFile(string path, long offset, Stream destination, TransferProgress progress)
        {
            lock (callLock)
            {
                uint id = Begin();
                try
                {
                    Send(new ReadFileRequest(path, offset), id);
                    long done = 0;
                    using (HashAlgorithm hash = SHA256.Create())
                    {
                        while (true)
                        {
                            Message m = Next();
                            FileDataReply data = m as FileDataReply;
                            if (data != null)
                            {
                                destination.Write(data.Data, 0, data.Data.Length);
                                hash.TransformBlock(data.Data, 0, data.Data.Length, null, 0);
                                done += data.Data.Length;
                                if (progress != null)
                                {
                                    progress(done);
                                }
                                continue;
                            }

                            FileEndReply end = Expect<FileEndReply>(m);
                            hash.TransformFinalBlock(new byte[0], 0, 0);
                            if (!SameHash(hash.Hash, end.Sha256))
                            {
                                throw new RemoteException(ErrorCode.IoError, Strings.Get("ErrorTransferChecksum"));
                            }
                            return end.Sha256;
                        }
                    }
                }
                finally
                {
                    End();
                }
            }
        }

        /// <summary>
        /// Uploads <paramref name="size"/> bytes from <paramref name="source"/> to the path on the peer. The
        /// server writes to a part file and renames it only after the SHA-256 matched.
        /// </summary>
        public void WriteFile(string path, Stream source, long size, DateTime created, DateTime modified,
            uint attributes, WriteMode mode, TransferProgress progress)
        {
            lock (callLock)
            {
                uint id = Begin();
                try
                {
                    WriteBeginRequest begin = new WriteBeginRequest();
                    begin.Path = path;
                    begin.Size = size;
                    begin.Created = created;
                    begin.Modified = modified;
                    begin.Attributes = attributes;
                    begin.Mode = mode;
                    Send(begin, id);
                    Expect<OkMessage>(Next());

                    using (HashAlgorithm hash = SHA256.Create())
                    {
                        byte[] buffer = new byte[FileServer.ChunkSize];
                        long sent = 0;
                        while (sent < size)
                        {
                            // The server reports a failure (full disk, no permission, cancel) as soon as it happens.
                            Message early = Poll();
                            if (early != null)
                            {
                                throw ToException(early);
                            }

                            int want = (int)Math.Min(buffer.Length, size - sent);
                            int got = source.Read(buffer, 0, want);
                            if (got <= 0)
                            {
                                Cancel();
                                throw new RemoteException(ErrorCode.IoError, "The source ended before " + size + " bytes");
                            }
                            WriteDataRequest chunk = new WriteDataRequest();
                            chunk.Data = new byte[got];
                            Buffer.BlockCopy(buffer, 0, chunk.Data, 0, got);
                            hash.TransformBlock(chunk.Data, 0, got, null, 0);
                            Send(chunk, id);
                            sent += got;
                            if (progress != null)
                            {
                                progress(sent);
                            }
                        }

                        hash.TransformFinalBlock(new byte[0], 0, 0);
                        WriteEndRequest end = new WriteEndRequest();
                        end.Sha256 = hash.Hash;
                        Send(end, id);
                    }

                    // The reply to WriteEnd; an early error the server sent before it is the same failure.
                    Expect<OkMessage>(Next());
                }
                finally
                {
                    End();
                }
            }
        }

        public void CreateDir(string path)
        {
            Simple(new CreateDirRequest(path));
        }

        /// <summary>Creates the folder (if needed) and gives it these times.</summary>
        public void CreateDir(string path, DateTime created, DateTime modified)
        {
            CreateDirRequest request = new CreateDirRequest(path);
            request.HasTimes = true;
            request.Created = created;
            request.Modified = modified;
            Simple(request);
        }

        public void Delete(string path)
        {
            Simple(new DeleteRequest(path));
        }

        public void Rename(string oldPath, string newPath)
        {
            Simple(new RenameRequest(oldPath, newPath));
        }

        private void Simple(Message request)
        {
            lock (callLock)
            {
                uint id = Begin();
                try
                {
                    Send(request, id);
                    Expect<OkMessage>(Next());
                }
                finally
                {
                    End();
                }
            }
        }

        private static bool SameHash(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
