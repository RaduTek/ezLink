using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EzLink;
using EzLink.Protocol;

namespace ezExplorer
{
    /// <summary>One file or folder to copy from the peer.</summary>
    internal sealed class DownloadEntry
    {
        /// <summary>Where it goes, relative to the destination, backslash separated ("Photos\2020\a.jpg").</summary>
        public string Relative = string.Empty;
        /// <summary>The full path on the peer.</summary>
        public string RemotePath = string.Empty;
        public bool IsFolder;
        public long Size;
        public DateTime Created;        // UTC
        public DateTime Modified;       // UTC
        public uint Attributes;
    }

    /// <summary>Everything a copy from the peer will transfer: the selection with folders expanded, parents first.</summary>
    internal sealed class DownloadPlan
    {
        public readonly List<DownloadEntry> Entries = new List<DownloadEntry>();
        public int FileCount;
        public long TotalBytes;

        public void Add(DownloadEntry entry)
        {
            Entries.Add(entry);
            if (!entry.IsFolder)
            {
                FileCount++;
                TotalBytes += entry.Size;
            }
        }

        /// <summary>A plan for one file taken from a listing, without asking the peer again.</summary>
        public static DownloadPlan ForFile(RemoteItem item)
        {
            DownloadEntry entry = new DownloadEntry();
            entry.Relative = item.Name;
            entry.RemotePath = item.Path;
            entry.Size = item.Size;
            entry.Modified = item.HasDate ? item.Modified.ToUniversalTime() : DateTime.UtcNow;
            entry.Created = entry.Modified;
            entry.Attributes = item.Attributes;
            DownloadPlan plan = new DownloadPlan();
            plan.Add(entry);
            return plan;
        }
    }

    /// <summary>Tells the plan builder whether to stop.</summary>
    internal delegate bool StopTest();

    /// <summary>
    /// Lists what the user selected on the peer (a <c>ListTree</c> per folder, a <c>Stat</c> per file) and turns
    /// it into a <see cref="DownloadPlan"/>. It may start before its dialog exists, so that a quick drag never
    /// shows one; if it runs longer than <see cref="Limit"/> it gives up with <see cref="TransferOutcome.TimedOut"/>.
    /// </summary>
    internal sealed class PrepareDownloadJob : TransferJob
    {
        public static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

        private readonly ITransferClientSource source;
        private readonly List<RemoteItem> items;
        private readonly TransferState ownState = new TransferState();
        private readonly ManualResetEvent done = new ManualResetEvent(false);
        private readonly object startLock = new object();

        private bool started;
        private volatile bool cancelled;
        private volatile bool timedOut;
        private volatile FileClient client;
        private System.Threading.Timer watchdog;
        private DownloadPlan plan;
        private TransferOutcome outcome = TransferOutcome.Cancelled;

        public PrepareDownloadJob(ITransferClientSource source, List<RemoteItem> items)
        {
            this.source = source;
            this.items = items;
        }

        public override TransferState SharedState
        {
            get { return ownState; }
        }

        /// <summary>The plan, valid once <see cref="WaitDone"/> returned true with outcome Completed.</summary>
        public DownloadPlan Plan
        {
            get { return plan; }
        }

        public TransferOutcome Outcome
        {
            get { return outcome; }
        }

        /// <summary>Starts the work on its own thread (once).</summary>
        public void Start()
        {
            lock (startLock)
            {
                if (started)
                {
                    return;
                }
                started = true;
                watchdog = new System.Threading.Timer(OnTimeout, null, (int)Limit.TotalMilliseconds,
                    Timeout.Infinite);
                Thread thread = new Thread(Work);
                thread.Name = "ezExplorer prepare";
                thread.IsBackground = true;
                thread.Start();
            }
        }

        public bool WaitDone(int milliseconds)
        {
            return done.WaitOne(milliseconds, false);
        }

        public override TransferOutcome Run(TransferState state, ITransferHost host)
        {
            Start();
            while (!done.WaitOne(100, false))
            {
            }
            return outcome;
        }

        public override void Cancel()
        {
            cancelled = true;
            AbortRequest();
        }

        private void OnTimeout(object unused)
        {
            timedOut = true;
            AbortRequest();
        }

        private void AbortRequest()
        {
            FileClient current = client;
            if (current != null)
            {
                current.Cancel();
            }
        }

        private void Work()
        {
            try
            {
                client = source.GetTransferClient();
                plan = BuildPlan(client, items, ownState, delegate { return cancelled || timedOut; });
                outcome = TransferOutcome.Completed;
            }
            catch (Exception ex)
            {
                FileClient current = client;
                if (timedOut)
                {
                    outcome = TransferOutcome.TimedOut;
                }
                else if (cancelled)
                {
                    outcome = TransferOutcome.Cancelled;
                }
                else if (current == null || current.Session.IsClosed)
                {
                    outcome = TransferOutcome.ConnectionLost;
                }
                else
                {
                    Log.Warn("Preparing the copy failed: {0}", ex.Message);
                    FailureMessage = ex.Message;
                    outcome = TransferOutcome.Failed;
                }
            }
            finally
            {
                System.Threading.Timer timer = watchdog;
                if (timer != null)
                {
                    timer.Dispose();
                }
                done.Set();
            }
        }

        /// <summary>
        /// Lists the items on the peer. Folders come before their contents. Throws if <paramref name="stop"/> says
        /// so, or if the peer fails a request.
        /// </summary>
        public static DownloadPlan BuildPlan(FileClient client, List<RemoteItem> items, TransferState state,
            StopTest stop)
        {
            DownloadPlan plan = new DownloadPlan();
            foreach (RemoteItem item in items)
            {
                if (stop())
                {
                    throw new IOException("stopped");
                }
                state.SetPreparing(item.Name);

                StatResultReply stat = client.Stat(item.Path);
                if (!stat.Exists)
                {
                    throw new FileNotFoundException(string.Format(Strings.Get("DownloadGone"), item.Name));
                }

                DownloadEntry root = new DownloadEntry();
                root.Relative = item.Name;
                root.RemotePath = item.Path;
                root.IsFolder = RemotePaths.IsFolder(stat.Attributes);
                root.Size = root.IsFolder ? 0 : stat.Size;
                root.Created = stat.Created;
                root.Modified = stat.Modified;
                root.Attributes = stat.Attributes;
                plan.Add(root);

                if (!root.IsFolder)
                {
                    continue;
                }
                foreach (FileEntry entry in client.ListTree(item.Path))
                {
                    DownloadEntry child = new DownloadEntry();
                    child.Relative = item.Name + "\\" + entry.Name;
                    child.RemotePath = RemotePaths.Combine(item.Path, entry.Name);
                    child.IsFolder = RemotePaths.IsFolder(entry.Attributes);
                    child.Size = child.IsFolder ? 0 : entry.Size;
                    child.Created = entry.Created;
                    child.Modified = entry.Modified;
                    child.Attributes = entry.Attributes;
                    plan.Add(child);
                    if ((plan.Entries.Count & 0xFF) == 0)
                    {
                        state.SetPreparing(child.Relative);
                        if (stop())
                        {
                            throw new IOException("stopped");
                        }
                    }
                }
            }
            return plan;
        }
    }
}
