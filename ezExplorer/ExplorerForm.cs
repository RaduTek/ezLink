using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// The remote browser window: toolbar, path box, folder tree and file list over one connection to the peer.
    /// All navigation goes through <see cref="NavigationController"/>; this class only renders what it says.
    /// </summary>
    internal sealed partial class ExplorerForm : Form, INavigationView
    {
        private readonly ExplorerArgs args;
        private readonly IpcWindow ipc;
        private readonly RemoteBrowser browser;
        private readonly NavigationController nav;
        private readonly ExplorerSettings settings;

        private string treeContextPath = string.Empty;

        private VirtualFileDataObject clipboardData;       // what we put on the clipboard, to take back on exit
        private bool downloadBusy;                          // a drag or copy is being prepared
        private int downloadDialogs;                        // download dialogs open right now
        private readonly List<string> tempRoots = new List<string>();

        private bool firstConnect = true;
        private readonly Timer detailsTimer = new Timer();
        private string listingPath;           // the folder the list shows
        private string pendingSelectName;      // select (and maybe rename) this item once the next listing shows
        private bool pendingRename;

        public ExplorerForm(ExplorerArgs args, IpcWindow ipc)
        {
            this.args = args;
            this.ipc = ipc;
            settings = ExplorerSettings.Load();

            Font = SystemFonts.MessageBoxFont; // Segoe UI on Vista and later, Tahoma on XP
            InitializeComponent();
            ApplyLocalizedContent();
            detailsTimer.Interval = 30;
            detailsTimer.Tick += OnDetailsTimer;
            ClientSize = settings.WindowSize;

            browser = new RemoteBrowser(args, this);
            browser.ShowHidden = settings.ShowHidden;
            browser.ShowSystem = settings.ShowSystem;
            browser.ConnectionChanged += OnConnectionChanged;
            nav = new NavigationController(browser, this);
            pathBar.Browser = browser;
            backButton.DropDown = backMenu;
            forwardButton.DropDown = forwardMenu;
            backButton.DropDownOpening += backButton_DropDownOpening;
            forwardButton.DropDownOpening += forwardButton_DropDownOpening;

            tree.FolderPicked += OnTreeFolderPicked;
            tree.FolderContextMenu += OnTreeContextMenu;
            tree.RenameRequested += OnTreeRename;
            list.ItemActivate += OnListActivate;
            list.RenameRequested += OnListRenameRequested;
            list.SelectionChanged += OnSelectionChanged;
            list.DropQuery += OnDropQuery;
            list.FilesDropped += OnFilesDropped;
            tree.DropQuery += OnDropQuery;
            tree.FilesDropped += OnFilesDropped;
            list.DragRequested += OnListDragRequested;
            tree.DragRequested += OnTreeDragRequested;

            ipc.MessageReceived += OnIpcMessage;
            SetView(settings.View);
            UpdateEnabled();
        }

        // ---- construction ----

        /// <summary>Texts and images that come from resources rather than the designer.</summary>
        private void ApplyLocalizedContent()
        {
            Text = string.Format(Strings.Get("ExplorerWindowTitle"), args.PeerName);

            bool highDpi = DpiInfo.HighDpi;
            toolStrip.ImageScalingSize = highDpi ? new Size(32, 32) : new Size(24, 24);

            backButton.Text = Strings.Get("ToolBack");
            backButton.Image = highDpi ? Properties.Resources.nav_back_32 : Properties.Resources.nav_back_24;
            forwardButton.Text = Strings.Get("ToolForward");
            forwardButton.Image = highDpi ? Properties.Resources.nav_forward_32 : Properties.Resources.nav_forward_24;
            upButton.Text = Strings.Get("ToolUp");
            upButton.Image = highDpi ? Properties.Resources.nav_up_32 : Properties.Resources.nav_up_24;
            refreshButton.Text = Strings.Get("ToolRefresh");
            refreshButton.Image = highDpi ? Properties.Resources.refresh_32 : Properties.Resources.refresh_24;
            ToolStripItem[] buttons = new ToolStripItem[] { backButton, forwardButton, upButton, refreshButton };
            foreach (ToolStripItem button in buttons)
            {
                button.ToolTipText = button.Text;
            }

            viewsButton.Text = Strings.Get("ToolViews");
            viewsButton.Image = highDpi ? Properties.Resources.view_details_32 : Properties.Resources.view_details_24;
            detailsItem.Text = Strings.Get("ViewDetails");
            iconsItem.Text = Strings.Get("ViewLargeIcons");
            listItem.Text = Strings.Get("ViewList");

            optionsButton.Text = Strings.Get("ToolOptions");
            optionsButton.ToolTipText = optionsButton.Text;
            optionsButton.Image = highDpi ? Properties.Resources.options_32 : Properties.Resources.options_24;

            pathBar.RootText = args.PeerName;
            //pathPanel.Height = pathBar.PreferredHeight + 10;

            detailsBar.ModifiedCaption = Strings.Get("DetailsModified");
            detailsBar.SizeCaption = Strings.Get("DetailsSize");

            listOpenItem.Text = Strings.Get("MenuOpenItem");
            listOpenItem.Font = new Font(listOpenItem.Font, FontStyle.Bold);
            listCopyItem.Text = Strings.Get("MenuCopy");
            listPasteItem.Text = Strings.Get("MenuPaste");
            listRenameItem.Text = Strings.Get("MenuRename");
            listDeleteItem.Text = Strings.Get("MenuDelete");
            listNewFolderItem.Text = Strings.Get("MenuNewFolder");
            listRefreshItem.Text = Strings.Get("MenuRefresh");

            treeOpenItem.Text = Strings.Get("MenuOpenItem");
            treeOpenItem.Font = new Font(treeOpenItem.Font, FontStyle.Bold);
            treeCopyItem.Text = Strings.Get("MenuCopy");
            treePasteItem.Text = Strings.Get("MenuPaste");
            treeRenameItem.Text = Strings.Get("MenuRename");
            treeDeleteItem.Text = Strings.Get("MenuDelete");
            treeNewFolderItem.Text = Strings.Get("MenuNewFolder");
            treeRefreshItem.Text = Strings.Get("MenuRefresh");
        }

        private void backButton_Click(object sender, EventArgs e)
        {
            nav.GoBack();
        }

        private void forwardButton_Click(object sender, EventArgs e)
        {
            nav.GoForward();
        }

        private const int HistoryMenuLength = 10;
        private readonly NativeToolStrip.NativeContextMenuStrip backMenu = new NativeToolStrip.NativeContextMenuStrip();
        private readonly NativeToolStrip.NativeContextMenuStrip forwardMenu = new NativeToolStrip.NativeContextMenuStrip();

        private void backButton_DropDownOpening(object sender, EventArgs e)
        {
            FillHistoryMenu(backMenu, nav.BackHistory(HistoryMenuLength), OnBackHistoryClick);
        }

        private void forwardButton_DropDownOpening(object sender, EventArgs e)
        {
            FillHistoryMenu(forwardMenu, nav.ForwardHistory(HistoryMenuLength), OnForwardHistoryClick);
        }

        /// <summary>One item per remembered folder, nearest first; the item's Tag is the number of steps to it.</summary>
        private void FillHistoryMenu(NativeToolStrip.NativeContextMenuStrip menu, List<string> folders, EventHandler click)
        {
            menu.Items.Clear();
            menu.ImageScalingSize = ShellIcons.SmallIconSize;
            for (int i = 0; i < folders.Count; i++)
            {
                string folder = folders[i];
                string text = RemotePaths.IsRoot(folder) ? args.PeerName : RemotePaths.GetName(folder);
                ToolStripMenuItem item = new ToolStripMenuItem(text.Replace("&", "&&"));
                item.ToolTipText = RemotePaths.IsRoot(folder) ? args.PeerName : folder;
                item.Image = ShellIcons.SmallIcon(ShellIcons.ForPath(folder));
                item.Tag = i + 1;
                item.Click += click;
                menu.Items.Add(item);
            }
        }

        private void OnBackHistoryClick(object sender, EventArgs e)
        {
            nav.GoBack((int)((ToolStripItem)sender).Tag);
        }

        private void OnForwardHistoryClick(object sender, EventArgs e)
        {
            nav.GoForward((int)((ToolStripItem)sender).Tag);
        }

        private void upButton_Click(object sender, EventArgs e)
        {
            nav.GoUp();
        }

        private void detailsItem_Click(object sender, EventArgs e)
        {
            SetView(View.Details);
        }

        private void iconsItem_Click(object sender, EventArgs e)
        {
            SetView(View.LargeIcon);
        }

        private void listItem_Click(object sender, EventArgs e)
        {
            SetView(View.List);
        }

        // ---- window lifetime ----

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (settings.Maximized)
            {
                WindowState = FormWindowState.Maximized;
            }
            int max = split.Width - split.Panel2MinSize - split.SplitterWidth;
            if (max > split.Panel1MinSize)
            {
                split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min(settings.SplitterDistance, max));
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (args.MainWindow != IntPtr.Zero)
            {
                IpcWindow.Send(args.MainWindow, ipc, IpcMessages.ExplorerHello, args.PeerId.ToString("D"));
            }
            ShowDetailsMessage(string.Format(Strings.Get("ExplorerConnecting"), args.PeerName));
            browser.Connect(OnFirstConnect);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            detailsTimer.Stop();
            detailsTimer.Dispose();
            settings.Maximized = WindowState == FormWindowState.Maximized;
            Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            settings.WindowSize = ClientSizeFor(bounds);
            settings.SplitterDistance = split.SplitterDistance;
            settings.Save();

            if (clipboardData != null)
            {
                // Data that is still on the clipboard would call into this process after it is gone.
                clipboardData.CancelAll();
                DragSource.ClearClipboardIf(clipboardData);
                clipboardData = null;
            }
            foreach (string root in tempRoots)
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                    // A program still has the opened file: leave it for the temp folder cleanup.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            if (args.MainWindow != IntPtr.Zero)
            {
                IpcWindow.Send(args.MainWindow, ipc, IpcMessages.ExplorerClosed, args.PeerId.ToString("D"));
            }
            browser.Dispose();
        }

        private Size ClientSizeFor(Rectangle bounds)
        {
            Size frame = Size - ClientSize;
            return new Size(Math.Max(400, bounds.Width - frame.Width), Math.Max(300, bounds.Height - frame.Height));
        }

        // ---- connecting ----

        private void OnFirstConnect(ConnectResult result, Exception error)
        {
            string name = args.PeerName;
            string problem = null;
            if (error != null)
            {
                Log.Warn("Connecting to {0} failed: {1}", name, error.Message);
                problem = string.Format(Strings.Get("ExplorerConnectFailed"), name, error.Message);
            }
            else if (result == ConnectResult.Denied)
            {
                problem = string.Format(Strings.Get("ExplorerDenied"), name);
            }
            else if (result == ConnectResult.VersionMismatch)
            {
                problem = string.Format(Strings.Get("ExplorerVersionMismatch"), name);
            }

            if (problem != null)
            {
                // Denied or timed out: say why and leave.
                MessageBox.Show(this, problem, Strings.Get("ExplorerTitle"), MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                Close();
                return;
            }

            firstConnect = false;
            tree.Initialize(browser);
            nav.NavigateTo(RemotePaths.Root);
            UpdateEnabled();
        }

        private void OnReconnect(ConnectResult result, Exception error)
        {
            if (error != null || result != ConnectResult.Connected)
            {
                Log.Warn("Reconnecting to {0} failed: {1}", args.PeerName,
                    error != null ? error.Message : result.ToString());
                UpdateEnabled();
                return;
            }
            tree.Initialize(browser);
            if (nav.Current == null)
            {
                nav.NavigateTo(RemotePaths.Root);
            }
            else
            {
                nav.Refresh();
            }
            UpdateEnabled();
        }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
        }

        /// <summary>Enables or disables everything that depends on the connection and the granted permissions.</summary>
        private void UpdateEnabled()
        {
            bool connected = browser.Connected;
            if (!connected && !firstConnect)
            {
                ShowDetailsMessage(string.Format(Strings.Get("ExplorerDisconnected"), args.PeerName));
            }
            else if (connected)
            {
                UpdateSelectionText();
            }
        }

        // ---- INavigationView ----

        void INavigationView.ShowListing(string path, List<RemoteItem> items)
        {
            listingPath = path;
            list.SetItems(items);
            if (pendingSelectName != null)
            {
                int index = list.IndexOfName(pendingSelectName);
                bool rename = pendingRename;
                pendingSelectName = null;
                pendingRename = false;
                if (index >= 0)
                {
                    list.SelectOnly(index);
                    if (rename)
                    {
                        list.Focus();
                        list.BeginRename(index);
                    }
                }
            }
            UpdateSelectionText();
        }

        void INavigationView.ShowPath(string path)
        {
            pathBar.ShowPath(path);
        }

        void INavigationView.UpdateNavigation(bool canBack, bool canForward, bool canUp)
        {
            backButton.Enabled = canBack;
            forwardButton.Enabled = canForward;
            upButton.Enabled = canUp;
        }

        void INavigationView.SyncTree(string path)
        {
            tree.SyncTo(path);
        }

        void INavigationView.NavigationFailed(string path, Exception error)
        {
            if (!browser.Connected)
            {
                UpdateEnabled(); // the details bar says we are disconnected
                return;
            }
            string shown = RemotePaths.IsRoot(path) ? args.PeerName : path;
            MessageBox.Show(this, string.Format(Strings.Get("ExplorerLoadFailed"), shown, error.Message),
                Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ---- toolbar, path box, views ----

        private void OnRefreshClick(object sender, EventArgs e)
        {
            RefreshAll();
        }

        private void optionsButton_Click(object sender, EventArgs e)
        {
            using (OptionsForm dialog = new OptionsForm(settings.ShowHidden, settings.ShowSystem))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                bool changed = dialog.ShowHidden != settings.ShowHidden || dialog.ShowSystem != settings.ShowSystem;
                settings.ShowHidden = dialog.ShowHidden;
                settings.ShowSystem = dialog.ShowSystem;
                browser.ShowHidden = settings.ShowHidden;
                browser.ShowSystem = settings.ShowSystem;
                if (changed)
                {
                    RefreshAll();
                }
            }
        }

        private void RefreshAll()
        {
            if (firstConnect)
            {
                return;
            }
            if (!browser.Connected)
            {
                ShowDetailsMessage(string.Format(Strings.Get("ExplorerReconnecting"), args.PeerName));
                browser.Reconnect(null, OnReconnect);
                return;
            }
            tree.RefreshAll(null);
            nav.Refresh();
        }

        private void OnPathNavigateRequested(string path)
        {
            nav.NavigateTo(path);
        }

        private void OnPathSubmitted(string typed)
        {
            string text = typed.Trim();
            string path = text.Length == 0 || string.Equals(text, args.PeerName, StringComparison.OrdinalIgnoreCase)
                ? RemotePaths.Root
                : RemotePaths.FromUserText(text);
            list.Focus();
            nav.NavigateTo(path);
        }

        private void OnPathEditCancelled(object sender, EventArgs e)
        {
            list.Focus();
        }

        private void SetView(View view)
        {
            list.View = view;
            settings.View = view;
            detailsItem.Checked = view == View.Details;
            iconsItem.Checked = view == View.LargeIcon;
            listItem.Checked = view == View.List;
        }

        // ---- tree and list events ----

        private void OnTreeFolderPicked(object sender, PathEventArgs e)
        {
            nav.NavigateTo(e.Path);
        }

        private void OnListActivate(object sender, EventArgs e)
        {
            OpenSelected();
        }

        private void OpenSelected()
        {
            List<RemoteItem> selected = list.SelectedRemoteItems;
            if (selected.Count == 1 && selected[0].IsFolder)
            {
                nav.NavigateTo(selected[0].Path);
            }
            else if (selected.Count == 1)
            {
                OpenRemoteFile(selected[0]);
            }
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectionText();
        }

        /// <summary>
        /// The details bar at the bottom, as in Windows 7: big icon, bold name and type on the left, date modified and
        /// size on the right. With nothing selected it describes the open folder.
        /// </summary>
        private void UpdateSelectionText()
        {
            // Selecting a range raises an event per change: redraw once after the burst, not for every one.
            if (!detailsTimer.Enabled)
            {
                detailsTimer.Start();
            }
        }

        private void OnDetailsTimer(object sender, EventArgs e)
        {
            detailsTimer.Stop();
            RefreshDetails();
        }

        private void RefreshDetails()
        {
            if (!browser.Connected)
            {
                return;
            }
            int count = list.SelectedIndices.Count;
            if (count == 0)
            {
                ShowFolderDetails();
                return;
            }
            if (count == 1)
            {
                ShowItemDetails(list.ItemAt(list.SelectedIndices[0]));
                return;
            }

            long bytes = 0;
            bool anyFile = false;
            foreach (int index in list.SelectedIndices)
            {
                RemoteItem item = list.ItemAt(index);
                if (!item.IsFolder)
                {
                    anyFile = true;
                    bytes += item.Size;
                }
            }
            SetDetails(list.ItemAt(list.SelectedIndices[0]).IconIndex,
                string.Format(Strings.Get("StatusManyItems"), count), string.Empty, string.Empty,
                anyFile ? RemoteListView.FormatSize(bytes) : string.Empty);
        }

        private void ShowItemDetails(RemoteItem item)
        {
            SetDetails(item.IconIndex, item.DisplayName, item.TypeName,
                item.HasDate ? item.Modified.ToString("g") : string.Empty,
                item.IsFolder ? string.Empty : RemoteListView.FormatSize(item.Size));
        }

        private void ShowFolderDetails()
        {
            string path = listingPath;
            if (path == null)
            {
                ShowDetailsMessage(string.Empty);
                return;
            }
            int icon;
            string name;
            if (RemotePaths.IsRoot(path))
            {
                icon = ShellIcons.ComputerIndex;
                name = args.PeerName;
            }
            else
            {
                string trimmed = path.TrimEnd('\\');
                bool drive = trimmed.IndexOf('\\') < 0;
                icon = drive ? ShellIcons.ForDrive((byte)DriveType.Fixed) : ShellIcons.FolderIndex;
                name = drive ? path : trimmed.Substring(trimmed.LastIndexOf('\\') + 1);
            }
            int items = list.ItemCount;
            SetDetails(icon, name,
                items == 1 ? Strings.Get("DetailsOneItem") : string.Format(Strings.Get("DetailsManyItems"), items),
                string.Empty, string.Empty);
        }

        /// <summary>A line of text (connecting, disconnected...) in place of the item details.</summary>
        private void ShowDetailsMessage(string text)
        {
            SetDetails(-1, text, string.Empty, string.Empty, string.Empty);
        }

        private void SetDetails(int iconIndex, string name, string type, string modified, string size)
        {
            detailsBar.SetDetails(iconIndex >= 0 ? ShellIcons.LargeIcon(iconIndex) : null, name, type, modified, size);
        }

        // ---- context menus ----

        private void OnListMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            List<RemoteItem> selected = list.SelectedRemoteItems;
            bool single = selected.Count == 1;
            bool anyDrive = selected.Exists(delegate(RemoteItem item) { return item.IsDrive; });
            bool inFolder = nav.Current != null && !RemotePaths.IsRoot(nav.Current);

            listOpenItem.Enabled = single && (selected[0].IsFolder || browser.CanRead);
            listCopyItem.Enabled = selected.Count > 0 && !anyDrive && browser.CanRead;
            listRenameItem.Enabled = single && !anyDrive && browser.CanWrite;
            listDeleteItem.Enabled = selected.Count > 0 && !anyDrive && browser.CanWrite;
            listNewFolderItem.Enabled = inFolder && browser.CanWrite;
            listRefreshItem.Enabled = true;
            listPasteItem.Enabled = CanPasteInto(ListPasteTarget());
        }

        private void OnTreeContextMenu(object sender, TreeContextEventArgs e)
        {
            treeContextPath = e.Path;
            treeOpenItem.Enabled = true;
            bool plainFolder = !e.IsDrive;
            treeCopyItem.Enabled = plainFolder && browser.CanRead;
            treeRenameItem.Enabled = plainFolder && browser.CanWrite;
            treeDeleteItem.Enabled = plainFolder && browser.CanWrite;
            treeNewFolderItem.Enabled = !RemotePaths.IsRoot(e.Path) && browser.CanWrite;
            treePasteItem.Enabled = CanPasteInto(e.Path);
            treeMenu.Show(tree, e.Location);
        }

        private void OnListOpen(object sender, EventArgs e)
        {
            OpenSelected();
        }

        private void OnListRename(object sender, EventArgs e)
        {
            RenameSelected();
        }

        private void OnListDelete(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private void OnListNewFolder(object sender, EventArgs e)
        {
            NewFolder(nav.Current);
        }

        private void OnListCopy(object sender, EventArgs e)
        {
            CopySelection(true);
        }

        private void OnTreeCopy(object sender, EventArgs e)
        {
            CopyToClipboard(new RemoteItem[] { FolderItem(treeContextPath) });
        }

        private void OnListPaste(object sender, EventArgs e)
        {
            PasteInto(ListPasteTarget());
        }

        private void OnTreePaste(object sender, EventArgs e)
        {
            PasteInto(treeContextPath);
        }

        private void OnTreeOpen(object sender, EventArgs e)
        {
            nav.NavigateTo(treeContextPath);
        }

        private void OnTreeMenuRename(object sender, EventArgs e)
        {
            tree.BeginRename(treeContextPath);
        }

        private void OnTreeDelete(object sender, EventArgs e)
        {
            List<string> paths = new List<string>();
            paths.Add(treeContextPath);
            DeletePaths(paths);
        }

        private void OnTreeNewFolder(object sender, EventArgs e)
        {
            NewFolder(treeContextPath);
        }

        // ---- keys ----

        /// <summary>
        /// The keyboard's Browser Back / Forward keys (and mouse side buttons) arrive as WM_APPCOMMAND, which
        /// child controls pass up to the form.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_APPCOMMAND = 0x0319;
            const int APPCOMMAND_BROWSER_BACKWARD = 1;
            const int APPCOMMAND_BROWSER_FORWARD = 2;

            if (m.Msg == WM_APPCOMMAND && !list.IsEditing && !tree.IsEditing)
            {
                int command = (int)(((long)m.LParam >> 16) & 0x0FFF);
                if (command == APPCOMMAND_BROWSER_BACKWARD)
                {
                    nav.GoBack();
                    m.Result = new IntPtr(1);
                    return;
                }
                if (command == APPCOMMAND_BROWSER_FORWARD)
                {
                    nav.GoForward();
                    m.Result = new IntPtr(1);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!list.IsEditing && !tree.IsEditing)
            {
                switch (keyData)
                {
                    case Keys.Alt | Keys.Up:
                        nav.GoUp();
                        return true;
                    case Keys.Alt | Keys.Left:
                        nav.GoBack();
                        return true;
                    case Keys.Alt | Keys.Right:
                        nav.GoForward();
                        return true;
                    case Keys.F5:
                        RefreshAll();
                        return true;
                    case Keys.Control | Keys.L:
                    case Keys.Alt | Keys.D:
                    case Keys.F4:
                        pathBar.BeginEdit();
                        return true;
                }

                if (list.Focused || tree.Focused)
                {
                    switch (keyData)
                    {
                        case Keys.Back:
                            nav.GoUp();
                            return true;
                        case Keys.F2:
                            if (list.Focused)
                            {
                                RenameSelected();
                            }
                            else if (tree.SelectedPath != null && browser.CanWrite)
                            {
                                tree.BeginRename(tree.SelectedPath);
                            }
                            return true;
                        case Keys.Delete:
                            if (list.Focused)
                            {
                                DeleteSelected();
                            }
                            else if (tree.SelectedPath != null && !RemotePaths.IsRoot(tree.SelectedPath)
                                && !RemotePaths.IsDriveRoot(tree.SelectedPath) && browser.CanWrite)
                            {
                                List<string> paths = new List<string>();
                                paths.Add(tree.SelectedPath);
                                DeletePaths(paths);
                            }
                            return true;
                        case Keys.Control | Keys.C:
                            CopySelection(list.Focused);
                            return true;
                        case Keys.Control | Keys.V:
                            PasteInto(list.Focused ? ListPasteTarget() : tree.SelectedPath);
                            return true;
                        case Keys.Control | Keys.A:
                            if (list.Focused)
                            {
                                list.SelectAllItems();
                                return true;
                            }
                            break;
                    }
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---- uploading: files from Explorer ----

        /// <summary>Whether files may be dropped on the folder (null: the current folder of the list).</summary>
        private void OnDropQuery(object sender, DropQueryEventArgs e)
        {
            string target = e.Path != null ? e.Path : nav.Current;
            e.Allowed = browser.CanWrite && target != null && !RemotePaths.IsRoot(target);
        }

        private void OnFilesDropped(object sender, FilesDroppedEventArgs e)
        {
            string target = e.Path != null ? e.Path : nav.Current;
            string[] files = e.Files;
            // Let the drop return to Explorer first: the transfer dialog is modal and would hold its drag loop.
            BeginInvoke(new MethodInvoker(delegate { StartUpload(files, target); }));
        }

        /// <summary>Explorer pastes into the selected folder if exactly one is selected, else into the open folder.</summary>
        private string ListPasteTarget()
        {
            List<RemoteItem> selected = list.SelectedRemoteItems;
            if (selected.Count == 1 && selected[0].IsFolder)
            {
                return selected[0].Path;
            }
            return nav.Current;
        }

        private bool CanPasteInto(string folder)
        {
            return browser.CanWrite && folder != null && !RemotePaths.IsRoot(folder) && ClipboardFiles().Length > 0;
        }

        private static string[] ClipboardFiles()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    System.Collections.Specialized.StringCollection files = Clipboard.GetFileDropList();
                    string[] result = new string[files.Count];
                    files.CopyTo(result, 0);
                    return result;
                }
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard is held by another program right now.
            }
            return new string[0];
        }

        private void PasteInto(string folder)
        {
            string[] files = ClipboardFiles();
            if (files.Length > 0)
            {
                StartUpload(files, folder);
            }
        }

        /// <summary>Copies local files and folders into a folder on the peer, inside the modal transfer dialog.</summary>
        private void StartUpload(string[] files, string folder)
        {
            if (files == null || files.Length == 0)
            {
                MessageBox.Show(this, Strings.Get("TransferNothing"), Strings.Get("ExplorerTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!browser.Connected)
            {
                return;
            }
            if (!browser.CanWrite)
            {
                MessageBox.Show(this, string.Format(Strings.Get("TransferNoWrite"), args.PeerName),
                    Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (folder == null || RemotePaths.IsRoot(folder))
            {
                MessageBox.Show(this, Strings.Get("TransferNoTarget"), Strings.Get("ExplorerTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TransferOutcome outcome;
            UploadJob job = new UploadJob(browser, files, folder);
            using (TransferDialog dialog = new TransferDialog(string.Format(Strings.Get("TransferTo"), args.PeerName), job))
            {
                dialog.ShowDialog(this);
                outcome = dialog.Outcome;
            }

            if (outcome == TransferOutcome.ConnectionLost || outcome == TransferOutcome.Failed)
            {
                browser.CheckLink();
            }
            UpdateEnabled();
            FinishChange(folder); // show what arrived, even after a cancel
        }

        // ---- downloading: files to Explorer ----

        /// <summary>A tree folder as an item, for the same code that handles the list's selection.</summary>
        private static RemoteItem FolderItem(string path)
        {
            RemoteItem item = new RemoteItem();
            item.Name = RemotePaths.GetName(path);
            item.DisplayName = item.Name;
            item.Path = path;
            item.IsFolder = true;
            item.Attributes = ShellNative.FILE_ATTRIBUTE_DIRECTORY;
            item.IconIndex = ShellIcons.FolderIndex;
            return item;
        }

        private List<RemoteItem> DownloadableSelection(bool fromList)
        {
            List<RemoteItem> items = new List<RemoteItem>();
            if (fromList)
            {
                foreach (RemoteItem item in list.SelectedRemoteItems)
                {
                    if (!item.IsDrive)
                    {
                        items.Add(item);
                    }
                }
            }
            else
            {
                string path = tree.SelectedPath;
                if (path != null && !RemotePaths.IsRoot(path) && !RemotePaths.IsDriveRoot(path))
                {
                    items.Add(FolderItem(path));
                }
            }
            return items;
        }

        private void OnListDragRequested(object sender, DragRequestEventArgs e)
        {
            DragOut(DownloadableSelection(true), e.Button);
        }

        private void OnTreeDragRequested(object sender, DragRequestEventArgs e)
        {
            List<RemoteItem> items = new List<RemoteItem>();
            items.Add(FolderItem(e.Path));
            DragOut(items, e.Button);
        }

        private bool CheckCanRead()
        {
            if (!browser.Connected)
            {
                return false;
            }
            if (!browser.CanRead)
            {
                MessageBox.Show(this, string.Format(Strings.Get("TransferNoRead"), args.PeerName),
                    Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        private VirtualFileDataObject NewDataObject(DownloadPlan plan)
        {
            VirtualFileDataObject data = new VirtualFileDataObject(plan, browser);
            data.OperationStarted += OnDownloadOperation;
            return data;
        }

        /// <summary>Drags the items into whatever window is under the cursor (normally Explorer).</summary>
        private void DragOut(List<RemoteItem> items, MouseButtons button)
        {
            if (items.Count == 0 || downloadBusy || !CheckCanRead())
            {
                return;
            }
            downloadBusy = true;
            try
            {
                bool slow;
                DownloadPlan plan = PrepareDownload(items, button, out slow);
                if (plan != null && (Control.MouseButtons & button) == 0)
                {
                    slow = true; // preparing took so long that the user let go: a drag can no longer start
                }
                if (slow)
                {
                    OfferCopyToFolder(items, plan);
                    return;
                }
                if (plan == null || plan.Entries.Count == 0)
                {
                    return;
                }
                DragSource.Drag(NewDataObject(plan), button, items);
            }
            finally
            {
                downloadBusy = false;
            }
        }

        /// <summary>Ctrl+C or Copy: puts the files on the clipboard without reading any of them yet.</summary>
        private void CopySelection(bool fromList)
        {
            CopyToClipboard(DownloadableSelection(fromList));
        }

        private void CopyToClipboard(IList<RemoteItem> chosen)
        {
            if (chosen.Count == 0 || downloadBusy || !CheckCanRead())
            {
                return;
            }
            List<RemoteItem> items = new List<RemoteItem>(chosen);
            downloadBusy = true;
            try
            {
                bool slow;
                DownloadPlan plan = PrepareDownload(items, MouseButtons.None, out slow);
                if (slow)
                {
                    OfferCopyToFolder(items, null);
                    return;
                }
                if (plan == null || plan.Entries.Count == 0)
                {
                    return;
                }

                VirtualFileDataObject data = NewDataObject(plan);
                if (DragSource.Copy(data))
                {
                    clipboardData = data;
                }
                else
                {
                    MessageBox.Show(this, Strings.Get("ClipboardBusy"), Strings.Get("ExplorerTitle"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                downloadBusy = false;
            }
        }

        /// <summary>
        /// Lists what is to be copied on the peer. A quick selection is ready before a dialog would be worth
        /// showing; otherwise the transfer dialog opens in its Preparing state. Returns null if there is no plan:
        /// cancelled, failed, or (<paramref name="slow"/>) not ready within the ten seconds a drag can wait.
        /// </summary>
        private DownloadPlan PrepareDownload(List<RemoteItem> items, MouseButtons held, out bool slow)
        {
            slow = false;
            PrepareDownloadJob job = new PrepareDownloadJob(browser, items);
            job.Start();

            DateTime started = DateTime.UtcNow;
            while (!job.WaitDone(15) && (DateTime.UtcNow - started).TotalMilliseconds < 400
                && (held == MouseButtons.None || (Control.MouseButtons & held) != 0))
            {
                Application.DoEvents(); // the button is still down: this must not turn into a dialog yet
            }

            bool dialogShown = false;
            if (!job.WaitDone(0))
            {
                dialogShown = true;
                using (TransferDialog dialog = new TransferDialog(string.Format(Strings.Get("TransferFrom"),
                    args.PeerName), job))
                {
                    dialog.ShowDialog(this);
                }
            }

            switch (job.Outcome)
            {
                case TransferOutcome.Completed:
                    return job.Plan;

                case TransferOutcome.TimedOut:
                    slow = true;
                    return null;

                case TransferOutcome.ConnectionLost:
                    browser.CheckLink();
                    UpdateEnabled();
                    if (!dialogShown) // the dialog says so itself
                    {
                        MessageBox.Show(this, Strings.Get("TransferConnectionLost"), Strings.Get("ExplorerTitle"),
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return null;

                case TransferOutcome.Failed:
                    browser.CheckLink();
                    if (!dialogShown)
                    {
                        MessageBox.Show(this, string.Format(Strings.Get("TransferFailed"), job.FailureMessage),
                            Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return null;

                default:
                    return null;
            }
        }

        /// <summary>A drag that could not start (too slow to prepare): copy to a folder chosen in a dialog instead.</summary>
        private void OfferCopyToFolder(List<RemoteItem> items, DownloadPlan plan)
        {
            if (MessageBox.Show(this, Strings.Get("TransferPrepSlow"), Strings.Get("ExplorerTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            using (FolderBrowserDialog picker = new FolderBrowserDialog())
            {
                picker.Description = string.Format(Strings.Get("CopyToFolderPrompt"), args.PeerName);
                if (picker.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                DownloadToFolderJob job = plan != null
                    ? new DownloadToFolderJob(browser, plan, picker.SelectedPath)
                    : new DownloadToFolderJob(browser, items, picker.SelectedPath);
                RunTransfer(string.Format(Strings.Get("TransferFrom"), args.PeerName), job);
            }
        }

        private TransferOutcome RunTransfer(string title, TransferJob job)
        {
            TransferOutcome outcome;
            using (TransferDialog dialog = new TransferDialog(title, job))
            {
                dialog.ShowDialog(this);
                outcome = dialog.Outcome;
            }
            if (outcome == TransferOutcome.ConnectionLost || outcome == TransferOutcome.Failed)
            {
                browser.CheckLink();
            }
            UpdateEnabled();
            return outcome;
        }

        /// <summary>Open: downloads the file to a temporary folder, then starts it with its associated program.</summary>
        private void OpenRemoteFile(RemoteItem item)
        {
            if (downloadBusy || !CheckCanRead())
            {
                return;
            }
            downloadBusy = true;
            try
            {
                string root = Path.Combine(Path.Combine(Path.GetTempPath(), "ezLink"), Path.GetRandomFileName());
                tempRoots.Add(root);
                DownloadToFolderJob job = new DownloadToFolderJob(browser, DownloadPlan.ForFile(item), root);
                if (RunTransfer(string.Format(Strings.Get("TransferFrom"), args.PeerName), job)
                    != TransferOutcome.Completed || job.FirstFilePath.Length == 0)
                {
                    return;
                }

                try
                {
                    ProcessStartInfo start = new ProcessStartInfo(job.FirstFilePath);
                    start.UseShellExecute = true;
                    Process.Start(start);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    MessageBox.Show(this, string.Format(Strings.Get("OpenFailed"), item.Name, ex.Message),
                        Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                downloadBusy = false;
            }
        }

        /// <summary>
        /// The drop target started pulling data (on any thread). The transfer dialog opens from the UI thread's
        /// own message loop, never from inside a stream read.
        /// </summary>
        private void OnDownloadOperation(DownloadOperation operation)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(new MethodInvoker(delegate { ShowDownloadDialog(operation); }));
                    return;
                }
            }
            catch (InvalidOperationException)
            {
            }
            operation.Cancel();
        }

        private void ShowDownloadDialog(DownloadOperation operation)
        {
            if (operation.IsEnded || IsDisposed)
            {
                return;
            }
            if (PumpGuard.Active || downloadDialogs > 0)
            {
                // Inside a stream read (which is waiting for this very call to return), or another copy's dialog
                // is up: try again a moment later, from outside.
                System.Threading.Timer retry = null;
                retry = new System.Threading.Timer(delegate(object unused)
                {
                    retry.Dispose();
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate { ShowDownloadDialog(operation); }));
                    }
                    catch (InvalidOperationException)
                    {
                        operation.Cancel();
                    }
                }, null, 100, System.Threading.Timeout.Infinite);
                return;
            }

            downloadDialogs++;
            try
            {
                RunTransfer(string.Format(Strings.Get("TransferFrom"), args.PeerName), operation);
            }
            finally
            {
                downloadDialogs--;
            }
        }

        // ---- file operations ----

        private void RenameSelected()
        {
            if (!browser.CanWrite || list.SelectedIndices.Count != 1)
            {
                return;
            }
            list.BeginRename(list.SelectedIndices[0]);
        }

        private void OnListRenameRequested(object sender, RenameEventArgs e)
        {
            DoRename(e.Item.Path, e.NewName);
        }

        private void OnTreeRename(object sender, TreeRenameEventArgs e)
        {
            DoRename(e.Path, e.NewName);
        }

        private void DoRename(string oldPath, string newName)
        {
            if (!browser.CanWrite)
            {
                return;
            }
            string name = newName.Trim();
            if (!RemotePaths.IsValidName(name))
            {
                MessageBox.Show(this, Strings.Get("InvalidFileName"), Strings.Get("ExplorerTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string parent = RemotePaths.GetParent(oldPath);
            string newPath = RemotePaths.Combine(parent, name);
            browser.Rename(oldPath, newPath, delegate(bool ok, Exception error)
            {
                if (error != null)
                {
                    MessageBox.Show(this, string.Format(Strings.Get("RenameFailed"), RemotePaths.GetName(oldPath),
                        error.Message), Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string current = nav.Current;
                if (current != null && RemotePaths.IsInside(current, oldPath))
                {
                    // The folder being shown (or one above it) was renamed: follow it to its new name.
                    string moved = newPath + current.Substring(oldPath.Length);
                    tree.RefreshFolder(parent, delegate { nav.NavigateTo(moved); });
                    return;
                }
                pendingSelectName = name;
                tree.RefreshFolder(parent, null);
                nav.Refresh();
            });
        }

        private void DeleteSelected()
        {
            if (!browser.CanWrite)
            {
                return;
            }
            List<string> paths = new List<string>();
            foreach (RemoteItem item in list.SelectedRemoteItems)
            {
                if (item.IsDrive)
                {
                    return;
                }
                paths.Add(item.Path);
            }
            DeletePaths(paths);
        }

        private void DeletePaths(List<string> paths)
        {
            if (paths.Count == 0 || !browser.CanWrite)
            {
                return;
            }
            string question = paths.Count == 1
                ? string.Format(Strings.Get("ConfirmDeleteOne"), RemotePaths.GetName(paths[0]), args.PeerName)
                : string.Format(Strings.Get("ConfirmDeleteMany"), paths.Count, args.PeerName);
            if (MessageBox.Show(this, question, Strings.Get("ConfirmDeleteTitle"), MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            DeleteNext(paths, 0);
        }

        private void DeleteNext(List<string> paths, int index)
        {
            if (index >= paths.Count)
            {
                FinishChange(RemotePaths.GetParent(paths[0]));
                return;
            }
            browser.Delete(paths[index], delegate(bool ok, Exception error)
            {
                if (error != null)
                {
                    MessageBox.Show(this, string.Format(Strings.Get("OperationFailed"), error.Message),
                        Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    FinishChange(RemotePaths.GetParent(paths[0])); // show what was deleted before the failure
                    return;
                }
                DeleteNext(paths, index + 1);
            });
        }

        /// <summary>Shows the result of a change in <paramref name="folder"/>: the list and the tree are reloaded.</summary>
        private void FinishChange(string folder)
        {
            if (folder != null)
            {
                tree.RefreshFolder(folder, null);
            }
            nav.Refresh(); // climbs to a parent that still exists if the current folder was deleted
        }

        /// <summary>
        /// Creates "New folder" (or "New folder (2)", ...) in a folder. The server treats creating an existing
        /// folder as success, so a free name is picked from the folder's listing first.
        /// </summary>
        private void NewFolder(string folder)
        {
            if (folder == null || RemotePaths.IsRoot(folder) || !browser.CanWrite)
            {
                return;
            }
            browser.ListDir(folder, delegate(List<RemoteItem> items, Exception error)
            {
                if (error != null)
                {
                    MessageBox.Show(this, string.Format(Strings.Get("OperationFailed"), error.Message),
                        Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string baseName = Strings.Get("NewFolderName");
                string name = baseName;
                for (int n = 2; Contains(items, name); n++)
                {
                    name = baseName + " (" + n + ")";
                }

                browser.CreateDir(RemotePaths.Combine(folder, name), delegate(bool ok, Exception createError)
                {
                    if (createError != null)
                    {
                        MessageBox.Show(this, string.Format(Strings.Get("OperationFailed"), createError.Message),
                            Strings.Get("ExplorerTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (nav.Current != null && RemotePaths.SamePath(nav.Current, folder))
                    {
                        pendingSelectName = name;
                        pendingRename = true;
                    }
                    FinishChange(folder);
                });
            });
        }

        private static bool Contains(List<RemoteItem> items, string name)
        {
            foreach (RemoteItem item in items)
            {
                if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- IPC from ezLink.exe ----

        private void OnIpcMessage(object sender, IpcMessageEventArgs e)
        {
            switch (e.Type)
            {
                case IpcMessages.ActivateExplorer:
                    BringToFront2();
                    break;

                case IpcMessages.PeerLost:
                    if (IsOurPeer(e.Payload))
                    {
                        Log.Info("ezLink says the peer is gone");
                        browser.MarkDisconnected();
                    }
                    break;

                case IpcMessages.PeerBack:
                    HandlePeerBack(e.Payload);
                    break;
            }
        }

        private bool IsOurPeer(string payload)
        {
            string id = payload;
            int cut = id.IndexOf(';');
            if (cut >= 0)
            {
                id = id.Substring(0, cut);
            }
            try
            {
                return new Guid(id.Trim()) == args.PeerId;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>Payload: "&lt;peer uuid&gt;;&lt;new ip&gt;".</summary>
        private void HandlePeerBack(string payload)
        {
            if (!IsOurPeer(payload) || firstConnect || browser.Connected)
            {
                return; // not for us, still starting, or the sessions survived
            }
            IPAddress address = null;
            int cut = payload.IndexOf(';');
            if (cut >= 0)
            {
                IPAddress.TryParse(payload.Substring(cut + 1).Trim(), out address);
            }
            ShowDetailsMessage(string.Format(Strings.Get("ExplorerReconnecting"), args.PeerName));
            browser.Reconnect(address, OnReconnect);
        }

        private void BringToFront2()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            // Windows only lets the foreground process take focus; a brief TopMost flip is the usual way round it.
            TopMost = true;
            TopMost = false;
            Activate();
        }
    }
}
