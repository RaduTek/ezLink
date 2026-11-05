using System;

namespace ezExplorer
{
    internal enum TransferOutcome
    {
        Completed,
        Cancelled,
        ConnectionLost,
        Failed,
        /// <summary>Preparing took longer than it may (a drag cannot wait for ever).</summary>
        TimedOut
    }

    internal enum ConflictChoice
    {
        Replace,
        Skip,
        KeepBoth
    }

    internal enum ErrorChoice
    {
        Retry,
        Skip,
        Cancel
    }

    /// <summary>The work a transfer dialog runs on its own thread.</summary>
    internal abstract class TransferJob
    {
        /// <summary>Why the job failed, when it returned <see cref="TransferOutcome.Failed"/>.</summary>
        public string FailureMessage = string.Empty;

        /// <summary>
        /// The state this job writes to, if it already has one (a job that started before its dialog opened, or
        /// one that is driven by someone else). Null: the dialog makes one and passes it to <see cref="Run"/>.
        /// </summary>
        public virtual TransferState SharedState
        {
            get { return null; }
        }

        /// <summary>Does the work. Runs on a worker thread; must return soon after <see cref="ITransferHost.Cancelled"/>.</summary>
        public abstract TransferOutcome Run(TransferState state, ITransferHost host);

        /// <summary>The user pressed Cancel: stop whatever request is in flight. Called on the UI thread.</summary>
        public abstract void Cancel();
    }

    /// <summary>Where a job gets its files session from (the explorer's connection; a loopback one in tests).</summary>
    internal interface ITransferClientSource
    {
        /// <summary>The session for transfers. May open one, so call it from a worker thread.</summary>
        EzLink.FileClient GetTransferClient();
    }

    /// <summary>What a running transfer job needs from the dialog that hosts it. Called on the job's thread.</summary>
    internal interface ITransferHost
    {
        /// <summary>True once the user pressed Cancel.</summary>
        bool Cancelled { get; }

        /// <summary>Asks what to do about a file that already exists. Blocks until the user answers.</summary>
        ConflictChoice AskConflict(string name, long existingSize, DateTime existingModified, long newSize,
            DateTime newModified);

        /// <summary>Asks what to do about a file that could not be copied. Blocks until the user answers.</summary>
        ErrorChoice AskError(string name, string message);
    }

    /// <summary>
    /// The numbers behind the transfer dialog. The job's thread writes them; the dialog's timer reads a
    /// <see cref="Snapshot"/> a few times a second, so the UI is never flooded with one update per chunk.
    /// </summary>
    internal sealed class TransferState
    {
        public struct Values
        {
            public bool Preparing;
            public string CurrentName;
            public string CurrentPath;
            public long FileDone;
            public long FileSize;
            public long TotalDone;
            public long TotalSize;
            public int ItemsDone;
            public int ItemsTotal;
        }

        private readonly object sync = new object();
        private Values values;
        private long baseline;      // bytes of the files that are already finished

        public TransferState()
        {
            values.Preparing = true;
            values.CurrentName = string.Empty;
            values.CurrentPath = string.Empty;
        }

        public Values Snapshot()
        {
            lock (sync)
            {
                return values;
            }
        }

        public void SetPreparing(string current)
        {
            lock (sync)
            {
                values.Preparing = true;
                values.CurrentName = current;
            }
        }

        public void SetTotals(int items, long bytes)
        {
            lock (sync)
            {
                values.Preparing = false;
                values.ItemsTotal = items;
                values.TotalSize = bytes;
                values.CurrentName = string.Empty;
            }
        }

        public void BeginItem(string name, string path, long size)
        {
            lock (sync)
            {
                values.CurrentName = name;
                values.CurrentPath = path;
                values.FileDone = 0;
                // A file that grew or shrank since it was counted keeps the total honest.
                values.FileSize = size;
            }
        }

        public void AdjustTotal(long delta)
        {
            lock (sync)
            {
                values.TotalSize += delta;
            }
        }

        public void SetFileDone(long done)
        {
            lock (sync)
            {
                values.FileDone = done;
                values.TotalDone = baseline + done;
            }
        }

        /// <summary>The current item is over. <paramref name="counted"/> is how many of its bytes count as done.</summary>
        public void EndItem(long counted)
        {
            lock (sync)
            {
                baseline += counted;
                values.TotalDone = baseline;
                values.FileDone = 0;
                values.ItemsDone++;
            }
        }
    }
}
