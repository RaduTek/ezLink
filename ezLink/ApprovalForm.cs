using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using EzLink;
using EzLink.Protocol;

namespace ezLink
{
    /// <summary>
    /// "NAME wants to access files on this computer." with the permissions to grant and the choices
    /// Allow once / Always allow / Deny. Shown in front of other windows. Closing it with the window's
    /// close button, letting it time out, or the requesting session going away is not a decision.
    /// </summary>
    internal sealed class ApprovalForm : Form
    {
        /// <summary>Just under the 2 minutes the requesting side waits.</summary>
        private static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(110);

        private readonly WaitHandle cancel;
        private readonly DateTime deadline = DateTime.UtcNow + PromptTimeout;
        private readonly CheckBox browseCopy = new CheckBox();
        private readonly CheckBox write = new CheckBox();
        private readonly Button allowOnce = new Button();
        private readonly Button alwaysAllow = new Button();
        private readonly Button deny = new Button();
        private readonly System.Windows.Forms.Timer watchdog = new System.Windows.Forms.Timer();
        private ApprovalResult result;

        public ApprovalForm(string peerName, WaitHandle cancel)
        {
            this.cancel = cancel;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = Strings.Get("AppName");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 196);

            Label headline = new Label();
            headline.Text = string.Format(Strings.Get("ApprovalHeadline"), peerName);
            headline.Font = new Font(Font, FontStyle.Bold);
            headline.Location = new Point(14, 14);
            headline.Size = new Size(412, 38);
            headline.AutoEllipsis = true;
            Controls.Add(headline);

            browseCopy.Text = Strings.Get("ApprovalBrowseCopy");
            browseCopy.Checked = true;
            browseCopy.AutoSize = true;
            browseCopy.Location = new Point(24, 58);
            browseCopy.CheckedChanged += OnPermissionsEdited;
            Controls.Add(browseCopy);

            write.Text = Strings.Get("ApprovalWrite");
            write.AutoSize = true;
            write.Location = new Point(24, 82);
            write.CheckedChanged += OnPermissionsEdited;
            Controls.Add(write);

            Label note = new Label();
            note.Text = Strings.Get("ApprovalNote");
            note.ForeColor = SystemColors.GrayText;
            note.Location = new Point(14, 110);
            note.Size = new Size(412, 34);
            Controls.Add(note);

            allowOnce.Text = Strings.Get("ApprovalAllowOnce");
            allowOnce.Location = new Point(14, 154);
            allowOnce.Size = new Size(110, 28);
            allowOnce.Click += delegate { Finish(ApprovalChoice.AllowOnce); };
            Controls.Add(allowOnce);

            alwaysAllow.Text = Strings.Get("ApprovalAlwaysAllow");
            alwaysAllow.Location = new Point(132, 154);
            alwaysAllow.Size = new Size(110, 28);
            alwaysAllow.Click += delegate { Finish(ApprovalChoice.AlwaysAllow); };
            Controls.Add(alwaysAllow);

            deny.Text = Strings.Get("ApprovalDeny");
            deny.Location = new Point(316, 154);
            deny.Size = new Size(110, 28);
            deny.Click += delegate { Finish(ApprovalChoice.Deny); };
            Controls.Add(deny);

            // Deny is the safe default for Enter; there is deliberately no Escape binding that decides anything.
            AcceptButton = deny;

            watchdog.Interval = 250;
            watchdog.Tick += OnWatchdog;
            watchdog.Start();
        }

        /// <summary>The user's decision, or null if the prompt ended without one.</summary>
        public ApprovalResult Result
        {
            get { return result; }
        }

        private Permissions Chosen
        {
            get
            {
                Permissions chosen = Permissions.None;
                if (browseCopy.Checked)
                {
                    chosen |= Permissions.Browse | Permissions.Read;
                }
                if (write.Checked)
                {
                    chosen |= Permissions.Write;
                }
                return chosen;
            }
        }

        private void OnPermissionsEdited(object sender, EventArgs e)
        {
            bool any = Chosen != Permissions.None;
            allowOnce.Enabled = any;
            alwaysAllow.Enabled = any;
        }

        private void Finish(ApprovalChoice choice)
        {
            result = new ApprovalResult(choice, choice == ApprovalChoice.Deny ? Permissions.None : Chosen);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnWatchdog(object sender, EventArgs e)
        {
            if (DateTime.UtcNow >= deadline || cancel.WaitOne(0, false))
            {
                result = null;
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                watchdog.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Shows the <see cref="ApprovalForm"/> on the UI thread for a session thread that is waiting.</summary>
    internal sealed class WinFormsApprovalPrompt : IApprovalPrompt
    {
        private volatile Control owner;

        /// <summary>The main window, whose message loop shows the prompts. Until set, every request is dismissed.</summary>
        public void Attach(Control mainWindow)
        {
            owner = mainWindow;
        }

        public ApprovalResult Ask(Guid peerId, string peerName, string sessionType, WaitHandle cancel)
        {
            Control window = owner;
            if (window == null)
            {
                return null;
            }

            ApprovalResult answer = null;
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                try
                {
                    window.BeginInvoke(new MethodInvoker(delegate
                    {
                        try
                        {
                            using (ApprovalForm form = new ApprovalForm(peerName, cancel))
                            {
                                form.ShowDialog();
                                answer = form.Result;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("The approval prompt failed", ex);
                        }
                        finally
                        {
                            done.Set();
                        }
                    }));
                }
                catch (InvalidOperationException)
                {
                    return null; // the window is gone: the program is exiting
                }
                done.WaitOne();
            }
            return answer;
        }
    }
}
