using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>What the peer may do right now; asked again for every request so edits apply at once.</summary>
    public delegate Permissions PermissionProvider();

    /// <summary>
    /// The file-transfer command set (files/1), server side: one instance per accepted files session.
    /// <para>
    /// Frames are parsed on the session's read thread and queued; a worker thread per session executes the
    /// requests one at a time, in order, so a Cancel can still be read while a transfer is running. The queue
    /// is bounded: when the disk is slower than the cable the read thread waits, which pushes back on TCP.
    /// </para>
    /// <para>
    /// Every request is checked against <see cref="PermissionProvider"/> before it touches the disk and every
    /// path goes through <see cref="RemotePath.Normalize"/>.
    /// </para>
    /// </summary>
    public sealed class FileServer : ICommandSet
    {
        public const int ChunkSize = 64 * 1024;
        public const string PartSuffix = ".ezlink-part";

        private const long MaxQueuedBytes = 8L * 1024 * 1024;
        private const int ListingBatchBytes = 256 * 1024;
        private const int ListingBatchEntries = 500;
        private const int MaxCancelledIds = 1024;
        private const FileAttributes WritableAttributes =
            FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive;

        private sealed class Job
        {
            public Message Message;
            public uint RequestId;
            public int Size;
        }

        /// <summary>An upload in progress on this session.</summary>
        private sealed class WriteState
        {
            public uint RequestId;
            public string Target;
            public string PartPath;
            public FileStream Stream;
            public long Expected;
            public long Received;
            public HashAlgorithm Hash;
            public DateTime Created;
            public DateTime Modified;
            public uint Attributes;
            public WriteMode Mode;
            public bool Failed;
            public ErrorCode FailCode;
            public string FailText;
        }

        private readonly PermissionProvider permissions;
        private readonly PeerSession session;
        private readonly object sync = new object();
        private readonly Queue<Job> queue = new Queue<Job>();
        private readonly Dictionary<uint, bool> cancelled = new Dictionary<uint, bool>();
        private readonly Thread worker;
        private long queuedBytes;
        private bool stopped;
        private WriteState write; // only the worker thread touches the contents; 'write' itself is guarded by sync

        public FileServer(PeerSession session, PermissionProvider permissions)
        {
            this.session = session;
            this.permissions = permissions;
            session.Register(this);
            session.CancelReceived += OnCancel;
            session.Closed += OnSessionClosed;

            worker = new Thread(Run);
            worker.IsBackground = true;
            worker.Name = "ezLink file server " + session.PeerName;
            worker.Start();
        }

        public byte CommandSet
        {
            get { return CommandSets.Files; }
        }

        public string Capability
        {
            get { return Capabilities.Files; }
        }

        // ---- receiving (read thread) ----

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
            if (message == null || !IsRequest(message))
            {
                from.Send(new UnsupportedMessage(frame.CommandSet, frame.Command), frame.RequestId);
                return;
            }

            Job job = new Job();
            job.Message = message;
            job.RequestId = frame.RequestId;
            job.Size = frame.Payload.Length + 64;

            lock (sync)
            {
                while (queuedBytes > MaxQueuedBytes && !stopped)
                {
                    Monitor.Wait(sync, 200);
                }
                if (stopped)
                {
                    return;
                }
                queue.Enqueue(job);
                queuedBytes += job.Size;
                Monitor.PulseAll(sync);
            }
        }

        private static bool IsRequest(Message m)
        {
            return m is GetRootsRequest || m is ListDirRequest || m is ListTreeRequest || m is StatRequest
                || m is ReadFileRequest || m is WriteBeginRequest || m is WriteDataRequest || m is WriteEndRequest
                || m is CreateDirRequest || m is DeleteRequest || m is RenameRequest;
        }

        private void OnCancel(object sender, MessageEventArgs e)
        {
            lock (sync)
            {
                if (cancelled.Count >= MaxCancelledIds)
                {
                    cancelled.Clear();
                }
                cancelled[e.RequestId] = true;
            }
        }

        private bool IsCancelled(uint requestId)
        {
            lock (sync)
            {
                return cancelled.ContainsKey(requestId);
            }
        }

        private void ForgetCancel(uint requestId)
        {
            lock (sync)
            {
                cancelled.Remove(requestId);
            }
        }

        private void OnSessionClosed(object sender, SessionClosedEventArgs e)
        {
            lock (sync)
            {
                stopped = true;
                Monitor.PulseAll(sync);
            }
        }

        // ---- the worker ----

        private void Run()
        {
            try
            {
                while (true)
                {
                    Job job;
                    lock (sync)
                    {
                        while (queue.Count == 0 && !stopped)
                        {
                            Monitor.Wait(sync);
                        }
                        if (queue.Count == 0)
                        {
                            break; // stopped and drained
                        }
                        if (stopped)
                        {
                            break;
                        }
                        job = queue.Dequeue();
                        queuedBytes -= job.Size;
                        Monitor.PulseAll(sync);
                    }
                    Execute(job);
                }
            }
            finally
            {
                AbortWrite(write);
            }
        }

        private void Execute(Job job)
        {
            uint id = job.RequestId;
            Message m = job.Message;
            try
            {
                if (m is GetRootsRequest)
                {
                    GetRoots(id);
                }
                else if (m is ListDirRequest)
                {
                    ListDir(id, (ListDirRequest)m);
                }
                else if (m is ListTreeRequest)
                {
                    ListTree(id, (ListTreeRequest)m);
                }
                else if (m is StatRequest)
                {
                    Stat(id, (StatRequest)m);
                }
                else if (m is ReadFileRequest)
                {
                    ReadFile(id, (ReadFileRequest)m);
                }
                else if (m is WriteBeginRequest)
                {
                    WriteBegin(id, (WriteBeginRequest)m);
                }
                else if (m is WriteDataRequest)
                {
                    WriteData(id, (WriteDataRequest)m);
                }
                else if (m is WriteEndRequest)
                {
                    WriteEnd(id, (WriteEndRequest)m);
                }
                else if (m is CreateDirRequest)
                {
                    CreateDir(id, (CreateDirRequest)m);
                }
                else if (m is DeleteRequest)
                {
                    Delete(id, (DeleteRequest)m);
                }
                else if (m is RenameRequest)
                {
                    Rename(id, (RenameRequest)m);
                }
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException)
                {
                    throw;
                }
                ReportFailure(id, ex);
            }
            finally
            {
                if (!(m is WriteDataRequest) && !(m is WriteBeginRequest))
                {
                    ForgetCancel(id);
                }
            }
        }

        // ---- replies ----

        private bool Reply(uint id, Message message)
        {
            return session.Send(message, id);
        }

        private void SendError(uint id, ErrorCode code, string text)
        {
            Reply(id, new ErrorMessage(code, text));
        }

        private static ErrorCode CodeFor(Exception ex)
        {
            if (ex is PathRejectedException || ex is UnauthorizedAccessException)
            {
                return ErrorCode.AccessDenied;
            }
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return ErrorCode.NotFound;
            }
            return ErrorCode.IoError;
        }

        private void ReportFailure(uint id, Exception ex)
        {
            if (ex is PathRejectedException)
            {
                Log.Warn("Refused a path from {0}", session.PeerName);
            }
            else if (!(ex is IOException) && !(ex is UnauthorizedAccessException))
            {
                Log.Error("File request failed", ex);
            }
            SendError(id, CodeFor(ex), ex.Message);
        }

        /// <summary>Checks the peer's permissions; sends PermissionNotGranted and returns false if none of 'needed' is held.</summary>
        private bool Require(uint id, Permissions needed)
        {
            Permissions granted = permissions();
            if ((granted & needed) != 0)
            {
                return true;
            }
            Log.Warn("{0} asked for something it is not allowed to do ({1} needed, {2} granted)",
                session.PeerName, needed, granted);
            SendError(id, ErrorCode.PermissionNotGranted, Strings.Get("ErrorPermissionNotGranted"));
            return false;
        }

        // ---- browsing ----

        private void GetRoots(uint id)
        {
            if (!Require(id, Permissions.Browse))
            {
                return;
            }
            RootsReply reply = new RootsReply();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if ((drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) || !drive.IsReady)
                    {
                        continue;
                    }
                    DriveEntry entry = new DriveEntry();
                    entry.Letter = char.ToUpperInvariant(drive.Name[0]);
                    entry.DriveType = (byte)drive.DriveType;
                    entry.Label = drive.VolumeLabel;
                    entry.FreeBytes = drive.AvailableFreeSpace;
                    entry.TotalBytes = drive.TotalSize;
                    reply.Drives.Add(entry);
                }
                catch (IOException)
                {
                    // Went away while we looked (media removed): leave it out.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            Reply(id, reply);
        }

        private static FileEntry ToEntry(FileSystemInfo info, string name)
        {
            FileEntry e = new FileEntry();
            e.Name = name;
            FileAttributes attributes = info.Attributes;
            e.Attributes = (uint)attributes;
            e.Size = (attributes & FileAttributes.Directory) != 0 ? 0 : ((FileInfo)info).Length;
            e.Created = info.CreationTimeUtc;
            e.Modified = info.LastWriteTimeUtc;
            return e;
        }

        private static int WireSize(FileEntry e)
        {
            return FileEntry.MinWireSize + Encoding.UTF8.GetByteCount(e.Name);
        }

        private void ListDir(uint id, ListDirRequest request)
        {
            if (!Require(id, Permissions.Browse))
            {
                return;
            }
            string path = RemotePath.Normalize(request.Path);
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                SendError(id, ErrorCode.NotFound, Strings.Get("ErrorFolderNotFound"));
                return;
            }

            FileSystemInfo[] items = dir.GetFileSystemInfos();
            DirListingReply batch = new DirListingReply();
            int bytes = 0;
            foreach (FileSystemInfo info in items)
            {
                FileEntry entry;
                try
                {
                    if (request.FoldersOnly && (info.Attributes & FileAttributes.Directory) == 0)
                    {
                        continue;
                    }
                    entry = ToEntry(info, info.Name);
                }
                catch (IOException)
                {
                    continue; // removed while listing
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                batch.Entries.Add(entry);
                bytes += WireSize(entry);
                if (batch.Entries.Count >= ListingBatchEntries || bytes >= ListingBatchBytes)
                {
                    batch.EndOfListing = false;
                    if (!Reply(id, batch))
                    {
                        return;
                    }
                    if (IsCancelled(id))
                    {
                        SendError(id, ErrorCode.Cancelled, Strings.Get("ErrorCancelled"));
                        return;
                    }
                    batch = new DirListingReply();
                    bytes = 0;
                }
            }
            batch.EndOfListing = true;
            Reply(id, batch);
        }

        private void ListTree(uint id, ListTreeRequest request)
        {
            if (!Require(id, Permissions.Read))
            {
                return;
            }
            string root = RemotePath.Normalize(request.Path);
            if (!Directory.Exists(root))
            {
                SendError(id, ErrorCode.NotFound, Strings.Get("ErrorFolderNotFound"));
                return;
            }

            // Parents are always sent before their children: a folder's entries are emitted when it is
            // enumerated, and its subfolders are enumerated later.
            Stack<string> pending = new Stack<string>();
            pending.Push(string.Empty);
            TreeEntriesReply batch = new TreeEntriesReply();
            int bytes = 0;
            bool first = true;

            while (pending.Count > 0)
            {
                string relative = pending.Pop();
                FileSystemInfo[] items;
                try
                {
                    items = new DirectoryInfo(relative.Length == 0 ? root : Path.Combine(root, relative)).GetFileSystemInfos();
                }
                catch (Exception ex)
                {
                    if (first)
                    {
                        throw; // the requested folder itself cannot be read
                    }
                    if (!(ex is IOException) && !(ex is UnauthorizedAccessException))
                    {
                        throw;
                    }
                    Log.Info("Skipping unreadable folder {0}: {1}", relative, ex.Message);
                    continue;
                }
                first = false;

                foreach (FileSystemInfo info in items)
                {
                    FileEntry entry;
                    bool descend;
                    try
                    {
                        string name = relative.Length == 0 ? info.Name : relative + "\\" + info.Name;
                        entry = ToEntry(info, name);
                        // Links to other folders are listed but not followed, so loops cannot happen.
                        descend = (info.Attributes & FileAttributes.Directory) != 0
                            && (info.Attributes & FileAttributes.ReparsePoint) == 0;
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }

                    if (descend)
                    {
                        pending.Push(entry.Name);
                    }
                    batch.Entries.Add(entry);
                    bytes += WireSize(entry);
                    if (batch.Entries.Count >= ListingBatchEntries || bytes >= ListingBatchBytes)
                    {
                        if (!Reply(id, batch))
                        {
                            return;
                        }
                        if (IsCancelled(id))
                        {
                            SendError(id, ErrorCode.Cancelled, Strings.Get("ErrorCancelled"));
                            return;
                        }
                        batch = new TreeEntriesReply();
                        bytes = 0;
                    }
                }
            }

            if (batch.Entries.Count > 0)
            {
                Reply(id, batch);
            }
            Reply(id, new TreeEndReply());
        }

        private void Stat(uint id, StatRequest request)
        {
            if (!Require(id, Permissions.Browse | Permissions.Write))
            {
                return;
            }
            string path = RemotePath.Normalize(request.Path);
            StatResultReply reply = new StatResultReply();
            FileSystemInfo info;
            if (Directory.Exists(path))
            {
                info = new DirectoryInfo(path);
            }
            else if (File.Exists(path))
            {
                info = new FileInfo(path);
            }
            else
            {
                Reply(id, reply); // Exists = false
                return;
            }
            FileEntry e = ToEntry(info, info.Name);
            reply.Exists = true;
            reply.Attributes = e.Attributes;
            reply.Size = e.Size;
            reply.Created = e.Created;
            reply.Modified = e.Modified;
            Reply(id, reply);
        }

        // ---- reading ----

        private void ReadFile(uint id, ReadFileRequest request)
        {
            if (!Require(id, Permissions.Read))
            {
                return;
            }
            string path = RemotePath.Normalize(request.Path);
            if (Directory.Exists(path))
            {
                SendError(id, ErrorCode.IoError, Strings.Get("ErrorIsFolder"));
                return;
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, ChunkSize))
            {
                if (request.Offset > stream.Length)
                {
                    SendError(id, ErrorCode.IoError, Strings.Get("ErrorOffsetBeyondEnd"));
                    return;
                }
                stream.Seek(request.Offset, SeekOrigin.Begin);

                using (HashAlgorithm hash = SHA256.Create())
                {
                    while (true)
                    {
                        if (IsCancelled(id))
                        {
                            SendError(id, ErrorCode.Cancelled, Strings.Get("ErrorCancelled"));
                            return;
                        }

                        byte[] data = new byte[ChunkSize];
                        int count = ReadFully(stream, data);
                        if (count == 0)
                        {
                            break;
                        }
                        if (count < data.Length)
                        {
                            byte[] shorter = new byte[count];
                            Buffer.BlockCopy(data, 0, shorter, 0, count);
                            data = shorter;
                        }
                        hash.TransformBlock(data, 0, count, null, 0);

                        FileDataReply chunk = new FileDataReply();
                        chunk.Data = data;
                        if (!Reply(id, chunk))
                        {
                            return; // the connection is gone
                        }
                    }

                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    FileEndReply end = new FileEndReply();
                    end.Sha256 = hash.Hash;
                    Reply(id, end);
                }
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = stream.Read(buffer, total, buffer.Length - total);
                if (n == 0)
                {
                    break;
                }
                total += n;
            }
            return total;
        }

        // ---- writing ----

        private void WriteBegin(uint id, WriteBeginRequest request)
        {
            // A new upload replaces one that was never finished.
            AbortWrite(write);
            lock (sync)
            {
                write = null;
            }

            if (!Require(id, Permissions.Write))
            {
                return;
            }
            if (IsCancelled(id))
            {
                SendError(id, ErrorCode.Cancelled, Strings.Get("ErrorCancelled"));
                return;
            }

            string target = RemotePath.Normalize(request.Path);
            if (RemotePath.IsDriveRoot(target) || Directory.Exists(target))
            {
                SendError(id, ErrorCode.Exists, Strings.Get("ErrorTargetIsFolder"));
                return;
            }
            if (request.Mode == WriteMode.FailIfExists && File.Exists(target))
            {
                SendError(id, ErrorCode.Exists, Strings.Get("ErrorAlreadyExists"));
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target));

            WriteState state = new WriteState();
            state.RequestId = id;
            state.Target = target;
            state.PartPath = target + PartSuffix;
            state.Expected = request.Size;
            state.Created = request.Created;
            state.Modified = request.Modified;
            state.Attributes = request.Attributes;
            state.Mode = request.Mode;
            state.Hash = SHA256.Create();
            // FileMode.Create truncates a leftover part file from an earlier interrupted upload.
            state.Stream = new FileStream(state.PartPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize);
            lock (sync)
            {
                write = state;
            }
            Reply(id, new OkMessage());
        }

        private void FailWrite(WriteState state, ErrorCode code, string text)
        {
            if (state.Failed)
            {
                return;
            }
            state.Failed = true;
            state.FailCode = code;
            state.FailText = text;
            AbortWrite(state);
            // Tell the sender now so it does not keep streaming into the void; the error is repeated at WriteEnd.
            SendError(state.RequestId, code, text);
        }

        /// <summary>Closes and deletes the part file. The state object stays so WriteEnd can still answer.</summary>
        private void AbortWrite(WriteState state)
        {
            if (state == null)
            {
                return;
            }
            try
            {
                if (state.Stream != null)
                {
                    state.Stream.Close();
                    state.Stream = null;
                }
                if (state.PartPath != null && File.Exists(state.PartPath))
                {
                    File.Delete(state.PartPath);
                }
            }
            catch (IOException ex)
            {
                Log.Warn("Could not remove {0}: {1}", state.PartPath, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warn("Could not remove {0}: {1}", state.PartPath, ex.Message);
            }
            if (state.Hash != null)
            {
                state.Hash.Clear();
                state.Hash = null;
            }
        }

        private void WriteData(uint id, WriteDataRequest request)
        {
            WriteState state = write;
            if (state == null || state.Failed)
            {
                return; // no upload, or it already failed and the sender has been told
            }
            if (IsCancelled(state.RequestId))
            {
                FailWrite(state, ErrorCode.Cancelled, Strings.Get("ErrorCancelled"));
                return;
            }
            if ((permissions() & Permissions.Write) == 0)
            {
                FailWrite(state, ErrorCode.PermissionNotGranted, Strings.Get("ErrorPermissionNotGranted"));
                return;
            }

            byte[] data = request.Data;
            if (state.Received + data.Length > state.Expected)
            {
                FailWrite(state, ErrorCode.IoError, Strings.Get("ErrorTooMuchData"));
                return;
            }
            try
            {
                state.Stream.Write(data, 0, data.Length);
            }
            catch (IOException ex)
            {
                FailWrite(state, ErrorCode.IoError, ex.Message); // typically a full disk
                return;
            }
            state.Hash.TransformBlock(data, 0, data.Length, null, 0);
            state.Received += data.Length;
        }

        private void WriteEnd(uint id, WriteEndRequest request)
        {
            WriteState state = write;
            lock (sync)
            {
                write = null;
            }
            try
            {
                if (state == null)
                {
                    SendError(id, IsCancelled(id) ? ErrorCode.Cancelled : ErrorCode.IoError,
                        Strings.Get(IsCancelled(id) ? "ErrorCancelled" : "ErrorNoUpload"));
                    return;
                }
                if (!state.Failed && IsCancelled(state.RequestId))
                {
                    FailWrite(state, ErrorCode.Cancelled, Strings.Get("ErrorCancelled"));
                }
                if (state.Failed)
                {
                    SendError(id, state.FailCode, state.FailText);
                    return;
                }
                if (!Require(id, Permissions.Write))
                {
                    AbortWrite(state);
                    return;
                }
                if (state.Received != state.Expected)
                {
                    AbortWrite(state);
                    SendError(id, ErrorCode.IoError, Strings.Get("ErrorIncompleteUpload"));
                    return;
                }

                state.Hash.TransformFinalBlock(new byte[0], 0, 0);
                if (!SameHash(state.Hash.Hash, request.Sha256))
                {
                    AbortWrite(state);
                    SendError(id, ErrorCode.IoError, Strings.Get("ErrorHashMismatch"));
                    return;
                }

                state.Stream.Flush();
                state.Stream.Close();
                state.Stream = null;
                Finish(state);
                Reply(id, new OkMessage());
            }
            catch (Exception ex)
            {
                if (state != null)
                {
                    AbortWrite(state);
                }
                if (ex is ThreadAbortException)
                {
                    throw;
                }
                ReportFailure(id, ex);
            }
            finally
            {
                if (state != null)
                {
                    ForgetCancel(state.RequestId);
                }
                ForgetCancel(id);
            }
        }

        /// <summary>Moves the finished part file into place, then applies the sender's times and attributes.</summary>
        private static void Finish(WriteState state)
        {
            string target = state.Target;
            if (File.Exists(target))
            {
                if (state.Mode == WriteMode.FailIfExists)
                {
                    throw new IOException(Strings.Get("ErrorAlreadyExists"));
                }
                File.SetAttributes(target, FileAttributes.Normal); // a read-only file may be overwritten
                File.Delete(target);
            }
            File.Move(state.PartPath, target);

            DateTime floor = new DateTime(1601, 1, 3, 0, 0, 0, DateTimeKind.Utc); // 0 means "unknown"
            if (state.Created > floor)
            {
                File.SetCreationTimeUtc(target, state.Created);
            }
            if (state.Modified > floor)
            {
                File.SetLastWriteTimeUtc(target, state.Modified);
            }
            File.SetAttributes(target, (FileAttributes)state.Attributes & WritableAttributes);
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

        // ---- file operations ----

        private void CreateDir(uint id, CreateDirRequest request)
        {
            if (!Require(id, Permissions.Write))
            {
                return;
            }
            string path = RemotePath.Normalize(request.Path);
            if (File.Exists(path))
            {
                SendError(id, ErrorCode.Exists, Strings.Get("ErrorAlreadyExists"));
                return;
            }
            Directory.CreateDirectory(path); // an existing folder is fine: creating it again is a no-op
            if (request.HasTimes)
            {
                DateTime floor = new DateTime(1601, 1, 3, 0, 0, 0, DateTimeKind.Utc); // 0 means "unknown"
                if (request.Created > floor)
                {
                    Directory.SetCreationTimeUtc(path, request.Created);
                }
                if (request.Modified > floor)
                {
                    Directory.SetLastWriteTimeUtc(path, request.Modified);
                }
            }
            Reply(id, new OkMessage());
        }

        private void Delete(uint id, DeleteRequest request)
        {
            if (!Require(id, Permissions.Write))
            {
                return;
            }
            string path = RemotePath.Normalize(request.Path);
            if (RemotePath.IsDriveRoot(path))
            {
                SendError(id, ErrorCode.AccessDenied, Strings.Get("ErrorPathNotAllowed"));
                return;
            }
            if (Directory.Exists(path))
            {
                DeleteFolder(new DirectoryInfo(path));
            }
            else if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            else
            {
                SendError(id, ErrorCode.NotFound, Strings.Get("ErrorNotFound"));
                return;
            }
            Reply(id, new OkMessage());
        }

        /// <summary>
        /// Deletes a folder and everything in it. A link to another folder is removed without touching what it
        /// points to.
        /// </summary>
        private static void DeleteFolder(DirectoryInfo folder)
        {
            if ((folder.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                folder.Delete(false);
                return;
            }
            foreach (FileSystemInfo item in folder.GetFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.Directory) != 0)
                {
                    DeleteFolder((DirectoryInfo)item);
                }
                else
                {
                    item.Attributes = FileAttributes.Normal;
                    item.Delete();
                }
            }
            folder.Attributes = FileAttributes.Directory;
            folder.Delete(false);
        }

        private void Rename(uint id, RenameRequest request)
        {
            if (!Require(id, Permissions.Write))
            {
                return;
            }
            string from = RemotePath.Normalize(request.Path);
            string to = RemotePath.Normalize(request.NewPath);
            if (RemotePath.IsDriveRoot(from) || RemotePath.IsDriveRoot(to))
            {
                SendError(id, ErrorCode.AccessDenied, Strings.Get("ErrorPathNotAllowed"));
                return;
            }

            bool isFolder = Directory.Exists(from);
            if (!isFolder && !File.Exists(from))
            {
                SendError(id, ErrorCode.NotFound, Strings.Get("ErrorNotFound"));
                return;
            }
            bool caseOnly = string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
            if (!caseOnly && (File.Exists(to) || Directory.Exists(to)))
            {
                SendError(id, ErrorCode.Exists, Strings.Get("ErrorAlreadyExists"));
                return;
            }

            if (isFolder)
            {
                Directory.Move(from, to);
            }
            else
            {
                File.Move(from, to);
            }
            Reply(id, new OkMessage());
        }
    }
}
