using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// The remote browser window: toolbar, path box, folder tree and file list over one connection to the peer.
    /// All navigation goes through <see cref="NavigationController"/>; this class only renders what it says.
    /// </summary>
    internal sealed class ExplorerForm : Form, INavigationView
    {
        private sealed class MenuSet
        {
            public readonly ContextMenuStrip Strip = new ContextMenuStrip();
            public ToolStripMenuItem Open;
            public ToolStripMenuItem Copy;
            public ToolStripMenuItem Paste;
            public ToolStripMenuItem Rename;
            public ToolStripMenuItem Delete;
            public ToolStripMenuItem NewFolder;
            public ToolStripMenuItem Refresh;
        }

        private readonly ExplorerArgs args;
        private readonly IpcWindow ipc;
        private readonly RemoteBrowser browser;
        private readonly NavigationController nav;
        private readonly ExplorerSettings settings;

        private readonly NativeToolStrip.NativeToolStrip toolStrip = new NativeToolStrip.NativeToolStrip();
        private readonly ToolStripButton backButton;
        private readonly ToolStripButton forwardButton;
        private readonly ToolStripButton upButton;
        private readonly ToolStripButton refreshButton;
        private readonly ToolStripDropDownButton viewsButton;
        private readonly ToolStripMenuItem detailsItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem iconsItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem listItem = new ToolStripMenuItem();

        private readonly Panel pathPanel = new Panel();
        private readonly TextBox pathBox = new TextBox();
        private readonly SplitContainer split = new SplitContainer();
        private readonly RemoteTreeView tree = new RemoteTreeView();
        private readonly RemoteListView list = new RemoteListView();
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel selectionLabel = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel connectionLabel = new ToolStripStatusLabel();

        private MenuSet listMenu;
        private MenuSet treeMenu;
        private string treeContextPath = string.Empty;

        private bool firstConnect = true;
        private string pendingSelectName;       // select (and maybe rename) this item once the next listing shows
        private bool pendingRename;

        public ExplorerForm(ExplorerArgs args, IpcWindow ipc)
        {
            this.args = args;
            this.ipc = ipc;
            settings = ExplorerSettings.Load();

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont; // Segoe UI on Vista and later, Tahoma on XP
            Text = string.Format(Strings.Get("ExplorerWindowTitle"), args.PeerName);
            StartPosition = FormStartPosition.WindowsDefaultLocation;
            ClientSize = settings.WindowSize;
            MinimumSize = new Size(480, 320);

            backButton = new ToolStripButton(Strings.Get("ToolBack"), ToolImages.Load("nav-back"));
            forwardButton = new ToolStripButton(Strings.Get("ToolForward"), ToolImages.Load("nav-forward"));
            upButton = new ToolStripButton(Strings.Get("ToolUp"), ToolImages.Load("nav-up"));
            refreshButton = new ToolStripButton(Strings.Get("ToolRefresh"), ToolImages.Load("nav-refresh"));
            viewsButton = new ToolStripDropDownButton(Strings.Get("ToolViews"), ToolImages.Load("view-details"));

            BuildToolStrip();
            BuildPathBar();
            BuildPanes();
            BuildStatusBar();
            listMenu = BuildMenu(OnListOpen, OnListRename, OnListDelete, OnListNewFolder, OnRefreshClick);
            treeMenu = BuildMenu(OnTreeOpen, OnTreeMenuRename, OnTreeDelete, OnTreeNewFolder, OnRefreshClick);
            list.ContextMenuStrip = listMenu.Strip;
            listMenu.Strip.Opening += OnListMenuOpening;

            // Dock order: the last control added is laid out first.
            Controls.Add(split);
            Controls.Add(pathPanel);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);

            pathBox.TabIndex = 0;
            tree.TabIndex = 1;
            list.TabIndex = 2;

            browser = new RemoteBrowser(args, this);
            browser.ConnectionChanged += OnConnectionChanged;
            nav = new NavigationController(browser, this);

            tree.FolderPicked += OnTreeFolderPicked;
            tree.FolderContextMenu += OnTreeContextMenu;
            tree.RenameRequested += OnTreeRename;
            list.ItemActivate += OnListActivate;
            list.RenameRequested += OnListRenameRequested;
            list.SelectionChanged += OnSelectionChanged;

            ipc.MessageReceived += OnIpcMessage;
            SetView(settings.View);
            UpdateEnabled();
        }

        // ---- construction ----

        private void BuildToolStrip()
        {
            toolStrip.AutoSize = false;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.ImageScalingSize = new Size(22, 22);
            toolStrip.Padding = new Padding(2, 0, 2, 0);
            toolStrip.Size = new Size(ClientSize.Width, 30);
            toolStrip.Dock = DockStyle.Top;
            toolStrip.Theme = NativeToolStrip.NativeToolStripTheme.Toolbar;
            toolStrip.ShowItemToolTips = true;

            ToolStripButton[] buttons = new ToolStripButton[] { backButton, forwardButton, upButton, refreshButton };
            foreach (ToolStripButton button in buttons)
            {
                button.DisplayStyle = ToolStripItemDisplayStyle.Image;
                button.ToolTipText = button.Text;
                button.ImageTransparentColor = Color.Magenta;
            }
            backButton.Click += delegate { nav.GoBack(); };
            forwardButton.Click += delegate { nav.GoForward(); };
            upButton.Click += delegate { nav.GoUp(); };
            refreshButton.Click += OnRefreshClick;

            viewsButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            viewsButton.ImageTransparentColor = Color.Magenta;
            detailsItem.Text = Strings.Get("ViewDetails");
            iconsItem.Text = Strings.Get("ViewLargeIcons");
            listItem.Text = Strings.Get("ViewList");
            detailsItem.Click += delegate { SetView(View.Details); };
            iconsItem.Click += delegate { SetView(View.LargeIcon); };
            listItem.Click += delegate { SetView(View.List); };
            viewsButton.DropDownItems.AddRange(new ToolStripItem[] { detailsItem, iconsItem, listItem });

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                backButton, forwardButton, upButton, new ToolStripSeparator(), refreshButton,
                new ToolStripSeparator(), viewsButton
            });
        }

        private void BuildPathBar()
        {
            pathPanel.Dock = DockStyle.Top;
            pathPanel.Padding = new Padding(4, 4, 4, 4);
            pathPanel.Height = pathBox.PreferredHeight + 10;
            pathBox.Dock = DockStyle.Fill;
            pathBox.KeyDown += OnPathKeyDown;
            pathPanel.Controls.Add(pathBox);
        }

        private void BuildPanes()
        {
            split.Dock = DockStyle.Fill;
            split.FixedPanel = FixedPanel.Panel1;
            tree.Dock = DockStyle.Fill;
            tree.BorderStyle = BorderStyle.None;
            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.None;
            split.Panel1.Controls.Add(tree);
            split.Panel2.Controls.Add(list);
        }

        private void BuildStatusBar()
        {
            selectionLabel.Spring = true;
            selectionLabel.TextAlign = ContentAlignment.MiddleLeft;
            connectionLabel.TextAlign = ContentAlignment.MiddleRight;
            statusStrip.Items.AddRange(new ToolStripItem[] { selectionLabel, connectionLabel });
        }

        private MenuSet BuildMenu(EventHandler open, EventHandler rename, EventHandler delete, EventHandler newFolder,
            EventHandler refresh)
        {
            MenuSet set = new MenuSet();
            set.Open = new ToolStripMenuItem(Strings.Get("MenuOpenItem"), null, open);
            set.Open.Font = new Font(set.Open.Font, FontStyle.Bold);
            // Copy and Paste arrive with the transfers (upload and download milestones).
            set.Copy = new ToolStripMenuItem(Strings.Get("MenuCopy"));
            set.Copy.Enabled = false;
            set.Paste = new ToolStripMenuItem(Strings.Get("MenuPaste"));
            set.Paste.Enabled = false;
            set.Rename = new ToolStripMenuItem(Strings.Get("MenuRename"), null, rename);
            set.Delete = new ToolStripMenuItem(Strings.Get("MenuDelete"), null, delete);
            set.NewFolder = new ToolStripMenuItem(Strings.Get("MenuNewFolder"), null, newFolder);
            set.Refresh = new ToolStripMenuItem(Strings.Get("MenuRefresh"), null, refresh);
            set.Strip.Items.AddRange(new ToolStripItem[]
            {
                set.Open, new ToolStripSeparator(), set.Copy, set.Paste, new ToolStripSeparator(),
                set.Rename, set.Delete, new ToolStripSeparator(), set.NewFolder, set.Refresh
            });
            return set;
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
            selectionLabel.Text = string.Format(Strings.Get("ExplorerConnecting"), args.PeerName);
            browser.Connect(OnFirstConnect);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            settings.Maximized = WindowState == FormWindowState.Maximized;
            Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            settings.WindowSize = ClientSizeFor(bounds);
            settings.SplitterDistance = split.SplitterDistance;
            settings.Save();

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
            connectionLabel.Text = connected || firstConnect
                ? string.Empty
                : string.Format(Strings.Get("ExplorerDisconnected"), args.PeerName);
            if (!connected && !firstConnect)
            {
                selectionLabel.Text = string.Empty;
            }
            else if (connected)
            {
                UpdateSelectionText();
            }
        }

        // ---- INavigationView ----

        void INavigationView.ShowListing(string path, List<RemoteItem> items)
        {
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
        }

        void INavigationView.ShowPath(string path)
        {
            pathBox.Text = RemotePaths.IsRoot(path) ? args.PeerName : path;
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
                UpdateEnabled(); // the status bar already says we are disconnected
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

        private void RefreshAll()
        {
            if (firstConnect)
            {
                return;
            }
            if (!browser.Connected)
            {
                selectionLabel.Text = string.Format(Strings.Get("ExplorerReconnecting"), args.PeerName);
                browser.Reconnect(null, OnReconnect);
                return;
            }
            tree.RefreshAll(null);
            nav.Refresh();
        }

        private void OnPathKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string text = pathBox.Text.Trim();
                string path = text.Length == 0 || string.Equals(text, args.PeerName, StringComparison.OrdinalIgnoreCase)
                    ? RemotePaths.Root
                    : RemotePaths.FromUserText(text);
                nav.NavigateTo(path);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                if (nav.Current != null)
                {
                    ((INavigationView)this).ShowPath(nav.Current);
                }
                list.Focus();
            }
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
            // Opening a file downloads it first, which needs the transfer dialog of a later milestone.
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectionText();
        }

        /// <summary>The status bar describes the selection only: how many items and, for files, how big.</summary>
        private void UpdateSelectionText()
        {
            if (!browser.Connected)
            {
                return;
            }
            int count = list.SelectedIndices.Count;
            if (count == 0)
            {
                selectionLabel.Text = string.Empty;
                return;
            }

            string text = count == 1
                ? Strings.Get("StatusOneItem")
                : string.Format(Strings.Get("StatusManyItems"), count);
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
            if (anyFile)
            {
                text = string.Format(Strings.Get("StatusSize"), text, RemoteListView.FormatSize(bytes));
            }
            selectionLabel.Text = text;
        }

        // ---- context menus ----

        private void OnListMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            List<RemoteItem> selected = list.SelectedRemoteItems;
            bool single = selected.Count == 1;
            bool anyDrive = selected.Exists(delegate(RemoteItem item) { return item.IsDrive; });
            bool inFolder = nav.Current != null && !RemotePaths.IsRoot(nav.Current);

            listMenu.Open.Enabled = single && selected[0].IsFolder;
            listMenu.Rename.Enabled = single && !anyDrive && browser.CanWrite;
            listMenu.Delete.Enabled = selected.Count > 0 && !anyDrive && browser.CanWrite;
            listMenu.NewFolder.Enabled = inFolder && browser.CanWrite;
            listMenu.Refresh.Enabled = true;
        }

        private void OnTreeContextMenu(object sender, TreeContextEventArgs e)
        {
            treeContextPath = e.Path;
            treeMenu.Open.Enabled = true;
            bool plainFolder = !e.IsDrive;
            treeMenu.Rename.Enabled = plainFolder && browser.CanWrite;
            treeMenu.Delete.Enabled = plainFolder && browser.CanWrite;
            treeMenu.NewFolder.Enabled = !RemotePaths.IsRoot(e.Path) && browser.CanWrite;
            treeMenu.Strip.Show(tree, e.Location);
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
            selectionLabel.Text = string.Format(Strings.Get("ExplorerReconnecting"), args.PeerName);
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
