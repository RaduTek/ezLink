using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// The contents of one remote file as an OLE <see cref="IStream"/>, for a drop target (usually Explorer) to
    /// read. A background thread streams the file from the peer into a read-ahead buffer of at least 4 MiB;
    /// <c>Read</c> only copies out of that buffer. The stream is read-only and forward-only: Seek answers
    /// position queries and nothing else.
    /// <para>
    /// The calls may arrive on the UI thread (OLE dispatches them to the thread that started the drag). While a
    /// <c>Read</c> there has to wait for the cable, it keeps pumping messages so the window and the transfer
    /// dialog stay alive.
    /// </para>
    /// </summary>
    [ComVisible(true)]
    internal sealed class RemoteFileStream : IStream
    {
        public const int BufferSize = 4 * 1024 * 1024;

        private readonly ITransferClientSource source;
        private readonly DownloadEntry entry;
        private readonly DownloadOperation operation;
        private readonly int uiThreadId;

        private readonly object sync = new object();
        private readonly byte[] ring = new byte[BufferSize];
        private int head;               // where the reader takes the next byte
        private int count;              // bytes waiting in the ring

        private bool started;
        private bool fillDone;          // the filler thread is over (finished, failed or aborted)
        private Exception fillError;    // why it failed, if it did
        private bool aborted;
        private bool reportedEnd;
        private long position;          // bytes handed to the reader
        private volatile FileClient client;

        public RemoteFileStream(ITransferClientSource source, DownloadEntry entry, DownloadOperation operation,
            int uiThreadId)
        {
            this.source = source;
            this.entry = entry;
            this.operation = operation;
            this.uiThreadId = uiThreadId;
        }

        // ---- the filler: peer to ring buffer ----

        private void StartFiller()
        {
            started = true;
            operation.StreamBegan(this, entry);
            Thread thread = new Thread(Fill);
            thread.Name = "ezExplorer download " + entry.Relative;
            thread.IsBackground = true;
            thread.Start();
        }

        private void Fill()
        {
            Exception error = null;
            try
            {
                FileClient c = source.GetTransferClient();
                client = c;
                if (aborted)
                {
                    return;
                }
                c.ReadFile(entry.RemotePath, 0, new Sink(this), null);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                lock (sync)
                {
                    fillDone = true;
                    // An abort is the reader's own doing, not a failure of the transfer.
                    fillError = aborted ? null : error;
                    Monitor.PulseAll(sync);
                }
            }
        }

        /// <summary>Called by the filler (through <see cref="Sink"/>) with data from the peer. Blocks while the ring is full.</summary>
        private void Push(byte[] data, int offset, int length)
        {
            while (length > 0)
            {
                lock (sync)
                {
                    while (count == ring.Length && !aborted)
                    {
                        Monitor.Wait(sync);
                    }
                    if (aborted)
                    {
                        throw new IOException(Strings.Get("ErrorCancelled"));
                    }

                    int tail = (head + count) % ring.Length;
                    int take = Math.Min(length, Math.Min(ring.Length - count, ring.Length - tail));
                    Buffer.BlockCopy(data, offset, ring, tail, take);
                    count += take;
                    offset += take;
                    length -= take;
                    Monitor.PulseAll(sync);
                }
            }
        }

        /// <summary>Stops the stream: a waiting or later Read fails, and the transfer from the peer is cancelled.</summary>
        public void Abort()
        {
            bool stopRequest;
            lock (sync)
            {
                stopRequest = started && !fillDone && !aborted;
                aborted = true;
                Monitor.PulseAll(sync);
            }
            FileClient c = client;
            if (stopRequest && c != null)
            {
                c.Cancel();
            }
        }

        private sealed class Sink : Stream
        {
            private readonly RemoteFileStream owner;

            public Sink(RemoteFileStream owner)
            {
                this.owner = owner;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                owner.Push(buffer, offset, count);
            }

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
        }

        // ---- the reader: ring buffer to the drop target ----

        /// <summary>
        /// Copies what is available, waiting for the first byte if there is none. Returns 0 only at the true end
        /// of the file; throws a COM error if the transfer failed or was stopped.
        /// </summary>
        private int ReadCore(byte[] target, int offset, int length)
        {
            if (length <= 0)
            {
                return 0;
            }
            bool onUiThread = Thread.CurrentThread.ManagedThreadId == uiThreadId;

            lock (sync)
            {
                if (!started && !aborted)
                {
                    StartFiller();
                }
            }

            while (true)
            {
                int got = -1;               // >= 0: bytes copied (0 is the true end)
                Exception failure = null;
                bool ended = false;
                long where = 0;

                lock (sync)
                {
                    if (aborted || operation.Stopped)
                    {
                        throw new COMException(Strings.Get("ErrorCancelled"), Ole.HR_CANCELLED);
                    }
                    if (count > 0)
                    {
                        int n = Math.Min(length, Math.Min(count, ring.Length - head));
                        Buffer.BlockCopy(ring, head, target, offset, n);
                        head = (head + n) % ring.Length;
                        count -= n;
                        position += n;
                        Monitor.PulseAll(sync);
                        got = n;
                        where = position;
                    }
                    else if (fillDone)
                    {
                        failure = fillError;
                        if (failure == null && !reportedEnd)
                        {
                            reportedEnd = true;
                            ended = true;
                        }
                        got = 0;
                        where = position;
                    }
                    else if (!onUiThread)
                    {
                        Monitor.Wait(sync, 50);
                    }
                    else
                    {
                        // On the UI thread: wait a short while, then let the window process its messages.
                        Monitor.Wait(sync, 15);
                    }
                }

                // The operation takes its own locks and may call back into Abort: never while holding ours.
                if (got > 0)
                {
                    operation.StreamProgress(this, where);
                    return got;
                }
                if (got == 0)
                {
                    if (failure != null)
                    {
                        operation.StreamFailed(this, failure);
                        throw new COMException(failure.Message, Ole.ERROR_READ_FAULT);
                    }
                    if (ended)
                    {
                        operation.StreamFinished(this, where);
                    }
                    return 0;
                }

                if (onUiThread)
                {
                    PumpGuard.Enter();
                    try
                    {
                        Application.DoEvents();
                    }
                    finally
                    {
                        PumpGuard.Leave();
                    }
                }
            }
        }

        // ---- IStream ----

        public void Read(byte[] pv, int cb, IntPtr pcbRead)
        {
            int total = ReadCore(pv, 0, cb);
            if (pcbRead != IntPtr.Zero)
            {
                Marshal.WriteInt32(pcbRead, total);
            }
        }

        public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
        {
            long target;
            switch (dwOrigin)
            {
                case 0: // STREAM_SEEK_SET
                    target = dlibMove;
                    break;
                case 1: // STREAM_SEEK_CUR
                    target = position + dlibMove;
                    break;
                case 2: // STREAM_SEEK_END
                    target = entry.Size + dlibMove;
                    break;
                default:
                    throw new COMException("Bad origin", Ole.STG_E_INVALIDFUNCTION);
            }
            if (target != position)
            {
                // The data is streamed once, front to back: only "where am I" questions can be answered.
                throw new COMException("The stream cannot seek", Ole.STG_E_INVALIDFUNCTION);
            }
            if (plibNewPosition != IntPtr.Zero)
            {
                Marshal.WriteInt64(plibNewPosition, position);
            }
        }

        public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
        {
            pstatstg = new System.Runtime.InteropServices.ComTypes.STATSTG();
            pstatstg.type = Ole.STGTY_STREAM;
            pstatstg.cbSize = entry.Size;
            pstatstg.grfMode = 0; // STGM_READ
            if ((grfStatFlag & Ole.STATFLAG_NONAME) == 0)
            {
                pstatstg.pwcsName = RemotePaths.GetName(entry.Relative); // the marshaler copies it to COM memory
            }
            long time = FileTimeOf(entry.Modified);
            pstatstg.mtime.dwLowDateTime = unchecked((int)(time & 0xFFFFFFFF));
            pstatstg.mtime.dwHighDateTime = unchecked((int)(time >> 32));
        }

        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
        {
            byte[] buffer = new byte[64 * 1024];
            long read = 0;
            long written = 0;
            while (cb > 0)
            {
                int n = ReadCore(buffer, 0, (int)Math.Min(buffer.Length, cb));
                if (n == 0)
                {
                    break;
                }
                read += n;
                cb -= n;
                pstm.Write(buffer, n, IntPtr.Zero);
                written += n;
            }
            if (pcbRead != IntPtr.Zero)
            {
                Marshal.WriteInt64(pcbRead, read);
            }
            if (pcbWritten != IntPtr.Zero)
            {
                Marshal.WriteInt64(pcbWritten, written);
            }
        }

        public void Write(byte[] pv, int cb, IntPtr pcbWritten)
        {
            throw new COMException("The stream is read-only", Ole.STG_E_ACCESSDENIED);
        }

        public void SetSize(long libNewSize)
        {
            throw new COMException("The stream is read-only", Ole.STG_E_ACCESSDENIED);
        }

        public void Commit(int grfCommitFlags)
        {
        }

        public void Revert()
        {
        }

        public void LockRegion(long libOffset, long cb, int dwLockType)
        {
            throw new COMException("Not supported", Ole.STG_E_INVALIDFUNCTION);
        }

        public void UnlockRegion(long libOffset, long cb, int dwLockType)
        {
            throw new COMException("Not supported", Ole.STG_E_INVALIDFUNCTION);
        }

        public void Clone(out IStream ppstm)
        {
            ppstm = null;
            throw new COMException("Not supported", Ole.E_NOTIMPL);
        }

        internal static long FileTimeOf(DateTime utc)
        {
            try
            {
                return utc.ToFileTimeUtc();
            }
            catch (ArgumentOutOfRangeException)
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Counts the message pumps running inside stream reads on the UI thread. A dialog must not open inside one:
    /// it would sit on top of a pending Read, which waits for the dialog to close.
    /// </summary>
    internal static class PumpGuard
    {
        private static int depth;

        public static bool Active
        {
            get { return Thread.VolatileRead(ref depth) > 0; }
        }

        public static void Enter()
        {
            Interlocked.Increment(ref depth);
        }

        public static void Leave()
        {
            Interlocked.Decrement(ref depth);
        }
    }
}
