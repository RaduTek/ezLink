using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// The modal window every transfer runs in. Nothing copies in the background and nothing reports through the
    /// status bar. The window is modal to the explorer only; it polls <see cref="TransferState"/> ten times a second.
    /// </summary>
    internal sealed partial class TransferDialog : Form, ITransferHost
    {
        private struct Sample
        {
            public long Ticks;
            public long Bytes;
        }

        private readonly TransferJob job;
        private readonly TransferState state;
        private readonly Queue<Sample> samples = new Queue<Sample>();

        private Thread worker;
        private volatile bool cancelled;
        private bool finished;
        private bool marquee = true;
        private ConflictChoice? applyToAll;
        private TransferOutcome outcome = TransferOutcome.Cancelled;

        public TransferDialog(string title, TransferJob job)
        {
            this.job = job;
            state = job.SharedState != null ? job.SharedState : new TransferState();

            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = title;
            headline.Text = title;
            headline.Font = new Font(Font, FontStyle.Bold);
            nameLabel.Text = Strings.Get("TransferPreparing");
            cancelButton.Text = Strings.Get("ButtonCancel");
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            RequestCancel();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            UpdateView();
        }

        public TransferOutcome Outcome
        {
            get { return outcome; }
        }

        public bool Cancelled
        {
            get { return cancelled; }
        }

        // ---- running ----

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            timer.Start();
            worker = new Thread(WorkerMain);
            worker.Name = "ezExplorer transfer";
            worker.IsBackground = true;
            worker.Start();
        }

        private void WorkerMain()
        {
            TransferOutcome result;
            try
            {
                result = job.Run(state, this);
            }
            catch (Exception ex)
            {
                Log.Error("Transfer failed", ex);
                job.FailureMessage = ex.Message;
                result = TransferOutcome.Failed;
            }
            if (cancelled && result == TransferOutcome.Completed)
            {
                result = TransferOutcome.Cancelled;
            }

            try
            {
                BeginInvoke(new MethodInvoker(delegate { Finish(result); }));
            }
            catch (InvalidOperationException)
            {
                // The dialog is already gone.
            }
        }

        private void Finish(TransferOutcome result)
        {
            timer.Stop();
            outcome = result;
            finished = true;
            UpdateView();

            if (result == TransferOutcome.ConnectionLost)
            {
                MessageBox.Show(this, Strings.Get("TransferConnectionLost"), Strings.Get("ExplorerTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (result == TransferOutcome.Failed)
            {
                MessageBox.Show(this, string.Format(Strings.Get("TransferFailed"), job.FailureMessage),
                    Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Close();
        }

        private void RequestCancel()
        {
            if (finished || cancelled)
            {
                return;
            }
            cancelled = true;
            cancelButton.Enabled = false;
            job.Cancel();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!finished)
            {
                // The explorer cannot go on while a copy runs: closing the window means "cancel", and the window
                // closes by itself once the job has stopped.
                e.Cancel = true;
                RequestCancel();
            }
            base.OnFormClosing(e);
        }

        // ---- the view ----

        private void UpdateView()
        {
            TransferState.Values v = state.Snapshot();
            if (v.Preparing)
            {
                nameLabel.Text = Strings.Get("TransferPreparing");
                pathLabel.Text = v.CurrentName;
                return;
            }

            if (marquee)
            {
                marquee = false;
                totalBar.Style = ProgressBarStyle.Continuous;
                fileBar.Visible = true;
            }

            nameLabel.Text = v.CurrentName;
            pathLabel.Text = v.CurrentPath;
            fileBar.Value = Scale(v.FileDone, v.FileSize);
            totalBar.Value = Scale(v.TotalDone, v.TotalSize);

            long now = DateTime.UtcNow.Ticks;
            Sample current = new Sample();
            current.Ticks = now;
            current.Bytes = v.TotalDone;
            samples.Enqueue(current);
            while (samples.Count > 1 && now - samples.Peek().Ticks > 5 * TimeSpan.TicksPerSecond)
            {
                samples.Dequeue();
            }

            string line1 = string.Format(Strings.Get("TransferBytesOf"), TransferFormat.Bytes(v.TotalDone),
                TransferFormat.Bytes(v.TotalSize)) + "   "
                + string.Format(Strings.Get("TransferItemsOf"), Math.Min(v.ItemsDone + 1, v.ItemsTotal), v.ItemsTotal);

            string line2 = Strings.Get("TransferCalculating");
            Sample first = samples.Peek();
            double seconds = (now - first.Ticks) / (double)TimeSpan.TicksPerSecond;
            if (seconds >= 1.0 && v.TotalDone > first.Bytes)
            {
                double speed = (v.TotalDone - first.Bytes) / seconds;
                double left = (v.TotalSize - v.TotalDone) / speed;
                line2 = string.Format(Strings.Get("TransferSpeed"), TransferFormat.Bytes((long)speed)) + "   "
                    + string.Format(Strings.Get("TransferRemaining"), TransferFormat.Time(left));
            }
            detail.Text = line1 + "\n" + line2;
        }

        private static int Scale(long done, long total)
        {
            if (total <= 0)
            {
                return 0;
            }
            long value = done * 1000 / total;
            return (int)Math.Max(0, Math.Min(1000, value));
        }

        // ---- ITransferHost (called on the job's thread) ----

        public ConflictChoice AskConflict(string name, long existingSize, DateTime existingModified, long newSize,
            DateTime newModified)
        {
            if (applyToAll.HasValue)
            {
                return applyToAll.Value;
            }
            ConflictChoice result = ConflictChoice.Skip;
            try
            {
                Invoke(new MethodInvoker(delegate
                {
                    using (ConflictDialog dialog = new ConflictDialog(name, existingSize, existingModified, newSize,
                        newModified))
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            result = dialog.Choice;
                            if (dialog.ApplyToAll)
                            {
                                applyToAll = result;
                            }
                        }
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                cancelled = true;
            }
            return result;
        }

        public ErrorChoice AskError(string name, string message)
        {
            ErrorChoice result = ErrorChoice.Cancel;
            try
            {
                Invoke(new MethodInvoker(delegate
                {
                    using (CopyErrorDialog dialog = new CopyErrorDialog(name, message))
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            result = dialog.Choice;
                        }
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                cancelled = true;
            }
            return result;
        }
    }

    internal static class TransferFormat
    {
        public static string Bytes(long bytes)
        {
            string[] units = new string[] { "bytes", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024.0 && unit < units.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }
            return unit == 0 ? bytes + " " + units[0] : value.ToString(value >= 100 ? "0" : "0.#") + " " + units[unit];
        }

        public static string Time(double seconds)
        {
            int total = (int)Math.Ceiling(Math.Max(0.0, seconds));
            if (total < 60)
            {
                return string.Format(Strings.Get("TimeSeconds"), total);
            }
            if (total < 3600)
            {
                return string.Format(Strings.Get("TimeMinutes"), total / 60, total % 60);
            }
            return string.Format(Strings.Get("TimeHours"), total / 3600, total % 3600 / 60);
        }
    }
}
