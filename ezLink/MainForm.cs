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
    internal sealed class MainForm : Form
    {
        private readonly LinkManager link;
        private readonly IpcWindow ipc;
        private readonly bool startHidden;

        private readonly NativeToolStrip.NativeToolStrip toolStrip = new NativeToolStrip.NativeToolStrip();
        private readonly ToolStripButton exploreButton;
        private readonly ToolStripButton syncButton;
        private readonly ToolStripButton optionsButton;
        private readonly Panel mainPanel = new Panel();
        private readonly PictureBox statusIcon = new PictureBox();
        private readonly Label statusLabel = new Label();
        private readonly Label peerLabel = new Label();
        private readonly Label weAllowLabel = new Label();
        private readonly Label peerAllowsLabel = new Label();

        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem openItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem exploreItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem exitItem = new ToolStripMenuItem();

        private bool allowVisible;
        private bool exiting;
        private LinkState lastState = LinkState.NoCable;
        private string lastConnectedName = string.Empty;

        public MainForm(LinkManager link, IpcWindow ipc, bool startHidden)
        {
            this.link = link;
            this.ipc = ipc;
            this.startHidden = startHidden;
            allowVisible = !startHidden;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont; // Segoe UI on Vista and later, Tahoma on XP
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = Strings.Get("AppName");
            ClientSize = new Size(420, 39 + 118);

            exploreButton = new ToolStripButton(Strings.Get("ToolExplore"), Images.Load("tool-explore"));
            syncButton = new ToolStripButton(Strings.Get("ToolSync"), Images.Load("tool-sync"));
            optionsButton = new ToolStripButton(Strings.Get("ToolOptions"), Images.Load("tool-options"));
            BuildToolStrip();
            BuildStatusArea();
            BuildTray();

            Controls.Add(mainPanel);
            Controls.Add(toolStrip);

            link.State.StateChanged += OnStateChanged;
            ipc.MessageReceived += OnIpcMessage;
            UpdateStatus();
        }

        // ---- construction ----

        private void BuildToolStrip()
        {
            toolStrip.AutoSize = false;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.ImageScalingSize = new Size(32, 32);
            toolStrip.Location = new Point(0, 0);
            toolStrip.Padding = new Padding(2, 0, 2, 0);
            toolStrip.Size = new Size(420, 39);
            toolStrip.Theme = NativeToolStrip.NativeToolStripTheme.Toolbar;
            toolStrip.ShowItemToolTips = true;

            exploreButton.ImageTransparentColor = Color.Magenta;
            exploreButton.Click += OnExploreClick;

            syncButton.ImageTransparentColor = Color.Magenta;
            syncButton.Enabled = false; // placeholder: sync is a later release
            syncButton.ToolTipText = Strings.Get("ToolSyncTip");

            optionsButton.Alignment = ToolStripItemAlignment.Right;
            optionsButton.ImageTransparentColor = Color.Magenta;
            optionsButton.Click += OnOptionsClick;

            toolStrip.Items.AddRange(new ToolStripItem[] { exploreButton, syncButton, optionsButton });
        }

        private void BuildStatusArea()
        {
            mainPanel.BackColor = SystemColors.ControlLightLight;
            mainPanel.Dock = DockStyle.Fill;

            statusIcon.Location = new Point(8, 8);
            statusIcon.Size = new Size(64, 64);
            statusIcon.SizeMode = PictureBoxSizeMode.StretchImage;
            statusIcon.TabStop = false;

            statusLabel.Font = new Font(Font.FontFamily, 11.25F, FontStyle.Regular, GraphicsUnit.Point);
            statusLabel.Location = new Point(80, 10);
            statusLabel.Size = new Size(328, 40);
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            peerLabel.Location = new Point(82, 52);
            peerLabel.Size = new Size(326, 18);
            peerLabel.AutoEllipsis = true;
            peerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            weAllowLabel.Location = new Point(82, 76);
            weAllowLabel.Size = new Size(326, 18);
            weAllowLabel.AutoEllipsis = true;
            weAllowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            peerAllowsLabel.Location = new Point(82, 96);
            peerAllowsLabel.Size = new Size(326, 18);
            peerAllowsLabel.AutoEllipsis = true;
            peerAllowsLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            mainPanel.Controls.Add(statusIcon);
            mainPanel.Controls.Add(statusLabel);
            mainPanel.Controls.Add(peerLabel);
            mainPanel.Controls.Add(weAllowLabel);
            mainPanel.Controls.Add(peerAllowsLabel);
        }

        private void BuildTray()
        {
            openItem.Text = Strings.Get("MenuOpen");
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            openItem.Click += delegate { ShowMain(); };

            exploreItem.Text = Strings.Get("MenuExploreNobody");
            exploreItem.Click += OnExploreClick;

            exitItem.Text = Strings.Get("MenuExit");
            exitItem.Click += delegate { ExitApplication(); };

            trayMenu.Items.Add(openItem);
            trayMenu.Items.Add(exploreItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitItem);

            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.DoubleClick += delegate { ShowMain(); };
            notifyIcon.Icon = Images.Tray(LinkState.NoCable);
            notifyIcon.Text = Strings.Get("AppName");
            notifyIcon.Visible = true;
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
            // The explorer notifications (ExplorerHello / ExplorerClosed) are handled once ezExplorer exists.
        }

        private void OnExploreClick(object sender, EventArgs e)
        {
            ExplorerLauncher.Launch(link, ipc.Handle, Visible ? (IWin32Window)this : null);
        }

        private void OnOptionsClick(object sender, EventArgs e)
        {
            using (OptionsForm options = new OptionsForm(link.MachineId, link.ComputerName))
            {
                options.ShowDialog(this);
            }
        }

        // ---- rendering the state ----

        private void UpdateStatus()
        {
            LinkStatus status = link.State.Status;
            bool connected = status.State == LinkState.Connected;

            statusIcon.Image = Images.Status(status.State);
            statusLabel.Text = status.Text;

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
