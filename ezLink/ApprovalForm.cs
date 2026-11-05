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
    internal sealed partial class ApprovalForm : Form
    {
        /// <summary>Just under the 2 minutes the requesting side waits.</summary>
        private static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(110);

        private readonly WaitHandle cancel;
        private readonly DateTime deadline = DateTime.UtcNow + PromptTimeout;
        private ApprovalResult result;

        public ApprovalForm(string peerName, WaitHandle cancel)
        {
            this.cancel = cancel;

            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = Strings.Get("AppName");
            headline.Text = string.Format(Strings.Get("ApprovalHeadline"), peerName);
            headline.Font = new Font(Font, FontStyle.Bold);
            browseCopy.Text = Strings.Get("ApprovalBrowseCopy");
            write.Text = Strings.Get("ApprovalWrite");
            note.Text = Strings.Get("ApprovalNote");
            allowOnce.Text = Strings.Get("ApprovalAllowOnce");
            alwaysAllow.Text = Strings.Get("ApprovalAlwaysAllow");
            deny.Text = Strings.Get("ApprovalDeny");

            // Deny is the safe default for Enter (AcceptButton); there is deliberately no Escape binding that
            // decides anything.
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

        private void allowOnce_Click(object sender, EventArgs e)
        {
            Finish(ApprovalChoice.AllowOnce);
        }

        private void alwaysAllow_Click(object sender, EventArgs e)
        {
            Finish(ApprovalChoice.AlwaysAllow);
        }

        private void deny_Click(object sender, EventArgs e)
        {
            Finish(ApprovalChoice.Deny);
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
