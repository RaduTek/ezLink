using System;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// A transfer job that works over the explorer's transfer session: the cancel, lost-connection and
    /// Retry / Skip / Cancel handling that uploads and downloads to a folder share.
    /// </summary>
    internal abstract class ClientJob : TransferJob
    {
        /// <summary>Unwinds a job to its end with the given outcome.</summary>
        protected sealed class Abort : Exception
        {
            public readonly TransferOutcome Outcome;

            public Abort(TransferOutcome outcome)
            {
                Outcome = outcome;
            }
        }

        private volatile FileClient client;

        protected FileClient Client
        {
            get { return client; }
            set { client = value; }
        }

        public override void Cancel()
        {
            FileClient current = client;
            if (current != null)
            {
                current.Cancel();
            }
        }

        protected bool IsLost()
        {
            FileClient current = client;
            return current == null || current.Session.IsClosed;
        }

        protected static void CheckCancel(ITransferHost host)
        {
            if (host.Cancelled)
            {
                throw new Abort(TransferOutcome.Cancelled);
            }
        }

        /// <summary>
        /// Maps an exception that ended a step to an abort (cancel or lost link), or returns normally if the
        /// failure is an ordinary one for the caller to handle.
        /// </summary>
        protected void ThrowIfEnded(ITransferHost host)
        {
            if (host.Cancelled)
            {
                throw new Abort(TransferOutcome.Cancelled);
            }
            if (IsLost())
            {
                throw new Abort(TransferOutcome.ConnectionLost);
            }
        }

        /// <summary>
        /// Runs one step, offering Retry / Skip / Cancel if it fails. Returns false if the user chose Skip.
        /// A lost connection or a cancel ends the whole job instead.
        /// </summary>
        protected bool Attempt(ITransferHost host, string name, MethodInvoker step)
        {
            while (true)
            {
                CheckCancel(host);
                try
                {
                    step();
                    return true;
                }
                catch (Abort)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ThrowIfEnded(host);
                    Log.Warn("Copying '{0}' failed: {1}", name, ex.Message);
                    ErrorChoice choice = host.AskError(name, ex.Message);
                    if (choice == ErrorChoice.Skip)
                    {
                        return false;
                    }
                    if (choice == ErrorChoice.Cancel)
                    {
                        throw new Abort(TransferOutcome.Cancelled);
                    }
                }
            }
        }

        /// <summary>The outcome for an exception nobody handled: cancel, lost link, or a failure with its message.</summary>
        protected TransferOutcome Unhandled(Exception ex, ITransferHost host, string what)
        {
            if (host.Cancelled)
            {
                return TransferOutcome.Cancelled;
            }
            if (IsLost())
            {
                return TransferOutcome.ConnectionLost;
            }
            Log.Warn("{0} failed: {1}", what, ex.Message);
            FailureMessage = ex.Message;
            return TransferOutcome.Failed;
        }
    }
}
