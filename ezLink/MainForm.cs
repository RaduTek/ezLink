using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;
using LinkState = EzLink.LinkState; // System.Windows.Forms has a LinkState too

namespace ezLink
{
    /// <summary>
    /// The ActiveSync-style status window (layout and toolbar rendering follow EasyMusicSync's main window) plus
    /// the tray icon. Minimising hides the window to the tray; closing it exits (after a confirmation while connected).
    /// </summary>
    internal sealed partial class MainForm : Form
    {
        private readonly LinkManager link;
        private readonly IpcWindow ipc;
        private readonly ExplorerLauncher explorers;
        private readonly bool startHidden;

        private bool allowVisible;
        private bool exiting;
        private LinkState lastState = LinkState.NoCable;
        private string lastConnectedName = string.Empty;

        public MainForm(LinkManager link, IpcWindow ipc, bool startHidden)
        {
            this.link = link;
            this.ipc = ipc;
            explorers = new ExplorerLauncher(ipc);
            this.startHidden = startHidden;
            allowVisible = !startHidden;

            Font = SystemFonts.MessageBoxFont; // Segoe UI on Vista and later, Tahoma on XP
            InitializeComponent();
            ApplyLocalizedContent();

            link.State.StateChanged += OnStateChanged;
            ipc.MessageReceived += OnIpcMessage;
            link.ControlOpened += OnControlOpened;
            link.ControlClosed += OnControlClosed;
            UpdateStatus();
        }

        // ---- construction ----

        /// <summary>Texts, images and fonts that come from resources or the system rather than the designer.</summary>
        private void ApplyLocalizedContent()
        {
            Text = Strings.Get("AppName");

            exploreButton.Text = Strings.Get("ToolExplore");
            exploreButton.Image = Images.Load("tool-explore");
            syncButton.Text = Strings.Get("ToolSync");
            syncButton.Image = Images.Load("tool-sync");
            syncButton.ToolTipText = Strings.Get("ToolSyncTip"); // placeholder: sync is a later release
            optionsButton.Text = Strings.Get("ToolOptions");
            optionsButton.Image = Images.Load("tool-options");

            statusLabel.Font = new Font(Font.FontFamily, 11.25F, FontStyle.Regular, GraphicsUnit.Point);
            fixButton.Text = Strings.Get("ErrorFixButton");

            openItem.Text = Strings.Get("MenuOpen");
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            exploreItem.Text = Strings.Get("MenuExploreNobody");
            exitItem.Text = Strings.Get("MenuExit");

            notifyIcon.Icon = Images.Tray(LinkState.NoCable);
            notifyIcon.Text = Strings.Get("AppName");
            notifyIcon.Visible = true;
        }

        private void openItem_Click(object sender, EventArgs e)
        {
            ShowMain();
        }

        private void exitItem_Click(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowMain();
        }

        // ---- showing, hiding, exiting ----

        /// <summary>When the program starts at logon (/minimized) the window stays hidden until asked for.</summary>
        protected override void SetVisibleCore(bool value)
        {
            if (!allowVisible)
            {
                value = false;
                if (!IsHandleCreated)
                {
                    CreateHandle(); // marshalling from the link threads needs a handle even while hidden
                }
            }
            base.SetVisibleCore(value);
        }

        public void ShowMain()
        {
            allowVisible = true;
            if (!Visible)
            {
                AnimateToTray(Bounds, false);
                Show();
            }
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Activate();
            BringToFront();
        }

        /// <summary>The close button exits the program, asking first while a connection is active.</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                LinkStatus status = link.State.Status;
                if (status.State == LinkState.Connected &&
                    MessageBox.Show(this, string.Format(Strings.Get("ConfirmExit"), status.PeerName),
                        Strings.Get("AppName"), MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                exiting = true;
            }
            base.OnFormClosing(e);
        }

        /// <summary>The minimise button hides the window to the tray, animating the caption toward the tray icons.</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_MINIMIZE)
            {
                AnimateToTray(Bounds, true);
                Hide();
                return;
            }
            base.WndProc(ref m);
        }

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        private const int IDANI_CAPTION = 3;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DrawAnimatedRects(IntPtr hwnd, int idAni, ref RECT from, ref RECT to);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string windowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        /// <summary>Plays the caption animation between the window and the taskbar notification area.</summary>
        private void AnimateToTray(Rectangle window, bool toTray)
        {
            RECT tray;
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            IntPtr notify = taskbar == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notify == IntPtr.Zero || !GetWindowRect(notify, out tray))
            {
                return; // no tray found: skip the animation
            }
            RECT win = new RECT();
            win.Left = window.Left;
            win.Top = window.Top;
            win.Right = window.Right;
            win.Bottom = window.Bottom;
            if (toTray)
            {
                DrawAnimatedRects(Handle, IDANI_CAPTION, ref win, ref tray);
            }
            else
            {
                DrawAnimatedRects(Handle, IDANI_CAPTION, ref tray, ref win);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            base.OnFormClosed(e);
        }

        private void ExitApplication()
        {
            exiting = true;
            notifyIcon.Visible = false;
            Application.Exit();
        }

        // ---- events ----

        private void OnStateChanged(object sender, EventArgs e)
        {
            // Raised on discovery/session threads.
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke(new MethodInvoker(UpdateStatus));
                }
                catch (InvalidOperationException)
                {
                    // The window is being torn down.
                }
            }
        }

        private void OnIpcMessage(object sender, IpcMessageEventArgs e)
        {
            if (e.Type == IpcMessages.ShowWindow)
            {
                ShowMain();
            }
            else
            {
                explorers.OnMessage(e); // ExplorerHello / ExplorerClosed
            }
        }

        // The control session events come from session threads; the explorers are told on the UI thread.
        private void OnControlOpened(object sender, PeerEventArgs e)
        {
            RunOnUi(delegate { explorers.PeerBack(e.Peer); });
        }

        private void OnControlClosed(object sender, PeerEventArgs e)
        {
            RunOnUi(delegate { explorers.PeerLost(e.Peer.MachineId); });
        }

        private void RunOnUi(MethodInvoker action)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch (InvalidOperationException)
                {
                    // The window is being torn down.
                }
            }
        }

        private void OnExploreClick(object sender, EventArgs e)
        {
            explorers.Launch(link, Visible ? (IWin32Window)this : null);
        }

        private void OnOptionsClick(object sender, EventArgs e)
        {
            using (OptionsForm options = new OptionsForm(link.MachineId, link.ComputerName, link.Trust))
            {
                options.ShowDialog(this);
            }
        }

        /// <summary>
        /// The fix for a duplicate machine ID (a cloned Windows image): the setup helper, elevated, gives this
        /// computer a new one. The ID is read once at startup, so ezLink exits afterwards.
        /// </summary>
        private void OnFixClick(object sender, EventArgs e)
        {
            if (link.State.Status.ErrorKind != LinkErrorKind.DuplicateUuid)
            {
                return;
            }
            if (MessageBox.Show(this, Strings.Get("FixUuidPrompt"), Strings.Get("AppName"),
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            int exitCode;
            if (!SetupLauncher.Run("/regenerate-uuid", this, out exitCode))
            {
                return;
            }
            if (exitCode != 0)
            {
                MessageBox.Show(this, string.Format(Strings.Get("FixUuidFailed"), exitCode), Strings.Get("AppName"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show(this, Strings.Get("FixUuidDone"), Strings.Get("AppName"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            ExitApplication();
        }

        // ---- rendering the state ----

        private void UpdateStatus()
        {
            LinkStatus status = link.State.Status;
            bool connected = status.State == LinkState.Connected;

            statusIcon.Image = Images.Status(status.State);
            statusLabel.Text = status.Text;
            fixButton.Visible = status.State == LinkState.Error && status.ErrorKind == LinkErrorKind.DuplicateUuid;

            bool showPeer = status.State == LinkState.Connected || status.State == LinkState.Connecting;
            peerLabel.Text = showPeer ? status.PeerName : string.Empty;
            weAllowLabel.Visible = connected;
            peerAllowsLabel.Visible = connected;
            if (connected)
            {
                weAllowLabel.Text = status.WeAllowText;
                peerAllowsLabel.Text = status.PeerAllowsText;
            }

            exploreButton.Enabled = connected;
            exploreItem.Enabled = connected;
            exploreItem.Text = connected
                ? string.Format(Strings.Get("MenuExplore"), status.PeerName)
                : Strings.Get("MenuExploreNobody");

            Icon icon = Images.Tray(status.State);
            notifyIcon.Icon = icon;
            Icon = icon;
            notifyIcon.Text = Truncate(status.Text, 63); // NotifyIcon.Text is limited to 63 characters

            if (connected)
            {
                lastConnectedName = status.PeerName;
                if (lastState != LinkState.Connected)
                {
                    notifyIcon.ShowBalloonTip(3000, Strings.Get("AppName"),
                        string.Format(Strings.Get("BalloonConnected"), status.PeerName), ToolTipIcon.Info);
                }
            }
            else if (lastState == LinkState.Connected)
            {
                notifyIcon.ShowBalloonTip(3000, Strings.Get("AppName"),
                    string.Format(Strings.Get("BalloonDisconnected"), lastConnectedName), ToolTipIcon.Info);
            }
            lastState = status.State;
        }

        private static string Truncate(string text, int length)
        {
            return text.Length <= length ? text : text.Substring(0, length - 1) + "…";
        }
    }
}
