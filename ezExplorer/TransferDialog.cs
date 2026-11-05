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
    internal sealed class TransferDialog : Form, ITransferHost
    {
        private struct Sample
        {
            public long Ticks;
            public long Bytes;
        }

        private readonly TransferJob job;
        private readonly TransferState state;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Label headline = new Label();
        private readonly Label nameLabel = new Label();
        private readonly Label pathLabel = new Label();
        private readonly ProgressBar fileBar = new ProgressBar();
        private readonly ProgressBar totalBar = new ProgressBar();
        private readonly Label detail = new Label();
        private readonly Button cancel = new Button();
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

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(480, 200);

            headline.Text = title;
            headline.Font = new Font(Font, FontStyle.Bold);
            headline.SetBounds(12, 12, 456, 20);

            nameLabel.AutoEllipsis = true;
            nameLabel.Text = Strings.Get("TransferPreparing");
            nameLabel.SetBounds(12, 38, 456, 18);
            pathLabel.AutoEllipsis = true;
            pathLabel.ForeColor = SystemColors.GrayText;
            pathLabel.SetBounds(12, 58, 456, 18);

            fileBar.SetBounds(12, 84, 456, 18);
            fileBar.Maximum = 1000;
            fileBar.Visible = false;
            totalBar.SetBounds(12, 108, 456, 18);
            totalBar.Maximum = 1000;
            totalBar.Style = ProgressBarStyle.Marquee;

            detail.AutoEllipsis = true;
            detail.SetBounds(12, 134, 456, 36);

            cancel.Text = Strings.Get("ButtonCancel");
            cancel.SetBounds(368, 162, 100, 28);
            cancel.Click += delegate { RequestCancel(); };
            CancelButton = cancel;

            Controls.AddRange(new Control[] { headline, nameLabel, pathLabel, fileBar, totalBar, detail, cancel });

            timer.Interval = 100;
            timer.Tick += delegate { UpdateView(); };
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
            cancel.Enabled = false;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
            }
            base.Dispose(disposing);
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
