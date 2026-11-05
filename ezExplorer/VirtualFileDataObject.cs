using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    internal delegate void DownloadOperationHandler(DownloadOperation operation);

    /// <summary>
    /// One copy out of the data object: from the moment the drop target calls StartOperation (or asks for the
    /// first stream) until EndOperation. It is the job the transfer dialog runs, but it does no work of its own:
    /// the drop target pulls the data, and this collects the numbers and the outcome.
    /// </summary>
    internal sealed class DownloadOperation : TransferJob
    {
        /// <summary>After a cancel, how long to wait for the drop target to call EndOperation before closing anyway.</summary>
        private const int CancelGraceMilliseconds = 1500;

        private readonly TransferState state = new TransferState();
        private readonly ManualResetEvent ended = new ManualResetEvent(false);
        private readonly object sync = new object();
        private readonly List<RemoteFileStream> streams = new List<RemoteFileStream>();
        private readonly ITransferClientSource source;

        private volatile bool cancelled;
        private volatile bool finished;
        private bool failedLink;
        private Exception firstError;
        private RemoteFileStream current;
        private TransferOutcome outcome = TransferOutcome.Cancelled;

        public DownloadOperation(DownloadPlan plan, ITransferClientSource source)
        {
            this.source = source;
            state.SetTotals(plan.FileCount, plan.TotalBytes);
        }

        public override TransferState SharedState
        {
            get { return state; }
        }

        /// <summary>True once the user cancelled or the operation is over: streams must fail from now on.</summary>
        public bool Stopped
        {
            get { return cancelled || finished; }
        }

        public bool IsEnded
        {
            get { return finished; }
        }

        public TransferOutcome Outcome
        {
            get { return outcome; }
        }

        // ---- the dialog's side ----

        public override TransferOutcome Run(TransferState unused, ITransferHost host)
        {
            while (!ended.WaitOne(100, false))
            {
                if (cancelled)
                {
                    // The drop target should notice its streams failing and call EndOperation; do not wait for ever.
                    if (!ended.WaitOne(CancelGraceMilliseconds, false))
                    {
                        Finish(Ole.E_ABORT);
                    }
                }
            }
            return outcome;
        }

        public override void Cancel()
        {
            cancelled = true;
            AbortStreams();
        }

        // ---- the streams' side ----

        public void StreamBegan(RemoteFileStream stream, DownloadEntry entry)
        {
            lock (sync)
            {
                streams.Add(stream);
                current = stream; // if two files are read at once, the newest is the one shown
            }
            state.BeginItem(RemotePaths.GetName(entry.Relative), RemotePaths.GetParent(entry.RemotePath), entry.Size);
        }

        public void StreamProgress(RemoteFileStream stream, long bytes)
        {
            lock (sync)
            {
                if (current != stream)
                {
                    return;
                }
            }
            state.SetFileDone(bytes);
        }

        public void StreamFinished(RemoteFileStream stream, long bytes)
        {
            lock (sync)
            {
                if (current == stream)
                {
                    current = null;
                }
                streams.Remove(stream);
            }
            state.EndItem(bytes);
        }

        public void StreamFailed(RemoteFileStream stream, Exception error)
        {
            lock (sync)
            {
                if (firstError == null)
                {
                    firstError = error;
                }
            }
            RemoteException remote = error as RemoteException;
            if (remote == null || remote.Code != EzLink.Protocol.ErrorCode.Cancelled)
            {
                // The session is shared by all files: if it is gone, say "connection lost" rather than the raw error.
                try
                {
                    FileClient client = source.GetTransferClient();
                    failedLink = client == null || client.Session.IsClosed;
                }
                catch (Exception)
                {
                    failedLink = true;
                }
            }
        }

        private void AbortStreams()
        {
            RemoteFileStream[] open;
            lock (sync)
            {
                open = streams.ToArray();
            }
            foreach (RemoteFileStream stream in open)
            {
                stream.Abort();
            }
        }

        /// <summary>The drop target is done (EndOperation): works out what happened and releases the dialog.</summary>
        public void Finish(int result)
        {
            lock (sync)
            {
                if (finished)
                {
                    return;
                }
                finished = true;

                if (cancelled)
                {
                    outcome = TransferOutcome.Cancelled;
                }
                else if (failedLink)
                {
                    outcome = TransferOutcome.ConnectionLost;
                }
                else if (firstError != null)
                {
                    FailureMessage = firstError.Message;
                    outcome = TransferOutcome.Failed;
                }
                else if (result < 0)
                {
                    outcome = TransferOutcome.Cancelled; // the drop target gave up (its own Cancel button, or an error)
                }
                else
                {
                    outcome = TransferOutcome.Completed;
                }
            }
            AbortStreams();
            ended.Set();
        }
    }

    /// <summary>
    /// A data object that offers files that live on the peer as <c>CFSTR_FILEDESCRIPTORW</c> plus one
    /// <c>CFSTR_FILECONTENTS</c> stream per file, for dropping into Explorer or pasting from the clipboard. The
    /// WinForms <c>DataObject</c> cannot serve FileContents per index, hence this class.
    /// <para>
    /// It implements <c>IAsyncOperation</c> and says it is asynchronous, so Explorer pulls the data on its own
    /// thread. It can serve any number of copies (a clipboard object is pasted many times): each StartOperation
    /// begins a new <see cref="DownloadOperation"/>.
    /// </para>
    /// </summary>
    [ComVisible(true)]
    internal sealed class VirtualFileDataObject : System.Runtime.InteropServices.ComTypes.IDataObject, IAsyncOperation
    {
        private sealed class Stored
        {
            public FORMATETC Format;
            public TYMED Tymed;     // how it is handed back: global memory or a stream
            public byte[] Data;
        }

        private readonly DownloadPlan plan;
        private readonly ITransferClientSource source;
        private readonly int uiThreadId;
        private readonly object sync = new object();
        private readonly short descriptorFormat;
        private readonly short contentsFormat;
        private readonly short effectFormat;
        private readonly List<Stored> stored = new List<Stored>();   // formats set by the shell's drag helper
        private byte[] descriptors;

        private bool inOperation;
        private DownloadOperation operation;

        /// <summary>Raised when a copy starts, on whatever thread the drop target called from.</summary>
        public event DownloadOperationHandler OperationStarted;

        public VirtualFileDataObject(DownloadPlan plan, ITransferClientSource source)
        {
            this.plan = plan;
            this.source = source;
            uiThreadId = Thread.CurrentThread.ManagedThreadId;
            descriptorFormat = (short)DataFormats.GetFormat(Ole.CFSTR_FILEDESCRIPTORW).Id;
            contentsFormat = (short)DataFormats.GetFormat(Ole.CFSTR_FILECONTENTS).Id;
            effectFormat = (short)DataFormats.GetFormat(Ole.CFSTR_PREFERREDDROPEFFECT).Id;
        }

        public DownloadPlan Plan
        {
            get { return plan; }
        }

        // ---- operations ----

        /// <summary>Begins a copy if none is running. Returns the running one.</summary>
        private DownloadOperation EnsureOperation(bool explicitStart)
        {
            DownloadOperation created = null;
            DownloadOperation result;
            lock (sync)
            {
                if (operation == null || operation.IsEnded)
                {
                    created = new DownloadOperation(plan, source);
                    operation = created;
                    inOperation = explicitStart;
                }
                else if (explicitStart)
                {
                    inOperation = true;
                }
                result = operation;
            }

            if (created != null)
            {
                DownloadOperationHandler handler = OperationStarted;
                if (handler != null)
                {
                    handler(created);
                }
            }
            return result;
        }

        public void StartOperation(IBindCtx reserved)
        {
            // A new copy always gets a new operation, even if a previous one never reported its end.
            lock (sync)
            {
                if (operation != null && !operation.IsEnded)
                {
                    operation.Finish(Ole.E_ABORT);
                }
            }
            EnsureOperation(true);
        }

        public void EndOperation(int result, IBindCtx reserved, int effects)
        {
            DownloadOperation ending;
            lock (sync)
            {
                ending = operation;
                inOperation = false;
            }
            if (ending != null)
            {
                ending.Finish(result);
            }
        }

        public void SetAsyncMode(bool doOpAsync)
        {
            // Ignored: the object is always asynchronous.
        }

        public void GetAsyncMode(out bool isOpAsync)
        {
            // Always asynchronous: the drop target pulls the data on its own thread.
            isOpAsync = true;
        }

        public void InOperation(out bool inAsyncOp)
        {
            lock (sync)
            {
                inAsyncOp = inOperation;
            }
        }

        /// <summary>Stops the copy that is running, if any (the window is closing).</summary>
        public void CancelAll()
        {
            DownloadOperation running;
            lock (sync)
            {
                running = operation;
            }
            if (running != null && !running.IsEnded)
            {
                running.Cancel();
            }
        }

        // ---- the descriptor list ----

        private byte[] BuildDescriptors()
        {
            lock (sync)
            {
                if (descriptors != null)
                {
                    return descriptors;
                }
            }

            MemoryStream buffer = new MemoryStream(4 + plan.Entries.Count * Ole.FileDescriptorSize);
            BinaryWriter w = new BinaryWriter(buffer);
            w.Write((uint)plan.Entries.Count);
            foreach (DownloadEntry entry in plan.Entries)
            {
                long time = RemoteFileStream.FileTimeOf(entry.Modified);
                uint flags = Ole.FD_ATTRIBUTES | Ole.FD_FILESIZE;
                if (time != 0)
                {
                    flags |= Ole.FD_WRITESTIME;
                }
                w.Write(flags);                         // dwFlags
                w.Write(new byte[16]);                  // clsid
                w.Write(0); w.Write(0);                 // sizel
                w.Write(0); w.Write(0);                 // pointl
                w.Write(entry.IsFolder ? entry.Attributes | ShellNative.FILE_ATTRIBUTE_DIRECTORY : entry.Attributes);
                w.Write(0L);                            // ftCreationTime
                w.Write(0L);                            // ftLastAccessTime
                w.Write(time);                          // ftLastWriteTime
                long size = entry.IsFolder ? 0 : entry.Size;
                w.Write((uint)(size >> 32));            // nFileSizeHigh
                w.Write((uint)(size & 0xFFFFFFFF));     // nFileSizeLow

                string name = entry.Relative;
                if (name.Length >= Ole.MaxPath)
                {
                    // cFileName holds 259 characters: keep the end of the path, which has the file's own name.
                    Log.Warn("Path too long for a drag, shortened: {0}", name);
                    name = name.Substring(name.Length - (Ole.MaxPath - 1));
                    int cut = name.IndexOf('\\');
                    if (cut >= 0)
                    {
                        name = name.Substring(cut + 1);
                    }
                }
                byte[] chars = Encoding.Unicode.GetBytes(name);
                w.Write(chars);
                w.Write(new byte[Ole.MaxPath * 2 - chars.Length]);
            }
            w.Flush();

            byte[] result = buffer.ToArray();
            lock (sync)
            {
                descriptors = result;
            }
            return result;
        }

        // ---- IDataObject ----

        public int QueryGetData(ref FORMATETC format)
        {
            if (format.dwAspect != DVASPECT.DVASPECT_CONTENT)
            {
                return Ole.DV_E_DVASPECT;
            }

            if (format.cfFormat == descriptorFormat || format.cfFormat == effectFormat)
            {
                return (format.tymed & TYMED.TYMED_HGLOBAL) != 0 ? Ole.S_OK : Ole.DV_E_TYMED;
            }
            if (format.cfFormat == contentsFormat)
            {
                if ((format.tymed & TYMED.TYMED_ISTREAM) == 0)
                {
                    return Ole.DV_E_TYMED;
                }
                if (format.lindex >= plan.Entries.Count)
                {
                    return Ole.DV_E_FORMATETC;
                }
                return Ole.S_OK;
            }

            lock (sync)
            {
                foreach (Stored item in stored)
                {
                    if (item.Format.cfFormat == format.cfFormat)
                    {
                        return (format.tymed & item.Tymed) != 0 ? Ole.S_OK : Ole.DV_E_TYMED;
                    }
                }
            }
            return Ole.DV_E_FORMATETC;
        }

        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            medium = new STGMEDIUM();
            int status = QueryGetData(ref format);
            if (status != Ole.S_OK)
            {
                throw new COMException("Format not available", status);
            }

            if (format.cfFormat == descriptorFormat)
            {
                medium.tymed = TYMED.TYMED_HGLOBAL;
                medium.unionmember = Ole.AllocGlobal(BuildDescriptors());
                return;
            }

            if (format.cfFormat == effectFormat)
            {
                medium.tymed = TYMED.TYMED_HGLOBAL;
                medium.unionmember = Ole.AllocGlobal(BitConverter.GetBytes(Ole.DROPEFFECT_COPY));
                return;
            }

            if (format.cfFormat == contentsFormat)
            {
                int index = format.lindex;
                if (index < 0 || index >= plan.Entries.Count)
                {
                    throw new COMException("No such file", Ole.DV_E_FORMATETC);
                }
                DownloadEntry entry = plan.Entries[index];
                if (entry.IsFolder)
                {
                    throw new COMException("A folder has no contents", Ole.DV_E_FORMATETC);
                }

                DownloadOperation running = EnsureOperation(false);
                RemoteFileStream stream = new RemoteFileStream(source, entry, running, uiThreadId);
                medium.tymed = TYMED.TYMED_ISTREAM;
                medium.unionmember = Marshal.GetComInterfaceForObject(stream, typeof(IStream));
                medium.pUnkForRelease = null;
                return;
            }

            lock (sync)
            {
                foreach (Stored item in stored)
                {
                    if (item.Format.cfFormat == format.cfFormat && (format.tymed & item.Tymed) != 0)
                    {
                        medium.tymed = item.Tymed;
                        medium.unionmember = item.Tymed == TYMED.TYMED_ISTREAM
                            ? Ole.NewStreamOn(item.Data)
                            : Ole.AllocGlobal(item.Data);
                        return;
                    }
                }
            }
            throw new COMException("Format not available", Ole.DV_E_FORMATETC);
        }

        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium)
        {
            throw new COMException("Not supported", Ole.E_NOTIMPL);
        }

        /// <summary>
        /// The shell's drag helper stores its drag image and context here, as global memory or as a stream. They
        /// are copied, so the caller's medium can be released at once.
        /// </summary>
        public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release)
        {
            byte[] data;
            if (medium.tymed == TYMED.TYMED_HGLOBAL)
            {
                data = Ole.ReadGlobal(medium.unionmember);
            }
            else if (medium.tymed == TYMED.TYMED_ISTREAM)
            {
                data = Ole.ReadStream(medium.unionmember);
            }
            else
            {
                throw new COMException("Only memory and streams can be stored", Ole.DV_E_TYMED);
            }
            if (data == null)
            {
                throw new COMException("Cannot read the data", Ole.E_FAIL);
            }

            Stored item = new Stored();
            item.Format = formatIn;
            item.Tymed = medium.tymed;
            item.Data = data;
            lock (sync)
            {
                short id = item.Format.cfFormat;
                stored.RemoveAll(delegate(Stored other) { return other.Format.cfFormat == id; });
                stored.Add(item);
            }
            if (release)
            {
                Ole.ReleaseStgMedium(ref medium);
            }
        }

        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
        {
            if (direction != DATADIR.DATADIR_GET)
            {
                throw new COMException("Not supported", Ole.E_NOTIMPL);
            }

            List<FORMATETC> formats = new List<FORMATETC>();
            formats.Add(Format(descriptorFormat, TYMED.TYMED_HGLOBAL, -1));
            formats.Add(Format(contentsFormat, TYMED.TYMED_ISTREAM, -1));
            formats.Add(Format(effectFormat, TYMED.TYMED_HGLOBAL, -1));
            lock (sync)
            {
                foreach (Stored item in stored)
                {
                    formats.Add(item.Format);
                }
            }
            return new FormatEnumerator(formats.ToArray());
        }

        private static FORMATETC Format(short id, TYMED tymed, int index)
        {
            FORMATETC format = new FORMATETC();
            format.cfFormat = id;
            format.dwAspect = DVASPECT.DVASPECT_CONTENT;
            format.lindex = index;
            format.ptd = IntPtr.Zero;
            format.tymed = tymed;
            return format;
        }

        public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
        {
            formatOut = formatIn;
            formatOut.ptd = IntPtr.Zero;
            return Ole.DATA_S_SAMEFORMATETC;
        }

        public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection)
        {
            connection = 0;
            return Ole.OLE_E_ADVISENOTSUPPORTED;
        }

        public void DUnadvise(int connection)
        {
            throw new COMException("Not supported", Ole.OLE_E_ADVISENOTSUPPORTED);
        }

        public int EnumDAdvise(out IEnumSTATDATA enumAdvise)
        {
            enumAdvise = null;
            return Ole.OLE_E_ADVISENOTSUPPORTED;
        }

        // ---- format enumerator ----

        [ComVisible(true)]
        private sealed class FormatEnumerator : IEnumFORMATETC
        {
            private readonly FORMATETC[] formats;
            private int position;

            public FormatEnumerator(FORMATETC[] formats)
            {
                this.formats = formats;
            }

            public int Next(int celt, FORMATETC[] rgelt, int[] pceltFetched)
            {
                int fetched = 0;
                while (fetched < celt && position < formats.Length)
                {
                    rgelt[fetched] = formats[position];
                    fetched++;
                    position++;
                }
                if (pceltFetched != null && pceltFetched.Length > 0)
                {
                    pceltFetched[0] = fetched;
                }
                return fetched == celt ? Ole.S_OK : Ole.S_FALSE;
            }

            public int Skip(int celt)
            {
                position = Math.Min(formats.Length, position + celt);
                return position < formats.Length ? Ole.S_OK : Ole.S_FALSE;
            }

            public int Reset()
            {
                position = 0;
                return Ole.S_OK;
            }

            public void Clone(out IEnumFORMATETC newEnum)
            {
                FormatEnumerator copy = new FormatEnumerator(formats);
                copy.position = position;
                newEnum = copy;
            }
        }
    }
}
