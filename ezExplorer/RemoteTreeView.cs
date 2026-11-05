using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EzLink;
using EzLink.Protocol;

namespace ezExplorer
{
    internal sealed class PathEventArgs : EventArgs
    {
        public readonly string Path;

        public PathEventArgs(string path)
        {
            Path = path;
        }
    }

    internal sealed class TreeContextEventArgs : EventArgs
    {
        public readonly string Path;
        public readonly bool IsDrive;
        public readonly Point Location;

        public TreeContextEventArgs(string path, bool isDrive, Point location)
        {
            Path = path;
            IsDrive = isDrive;
            Location = location;
        }
    }

    internal sealed class TreeRenameEventArgs : EventArgs
    {
        public readonly string Path;
        public readonly string NewName;

        public TreeRenameEventArgs(string path, string newName)
        {
            Path = path;
            NewName = newName;
        }
    }

    /// <summary>
    /// The folder tree: the peer's computer name, its drives, and folders only below them. Folders are loaded the
    /// first time they are expanded; until then a node carries a placeholder child so it shows an expand button.
    /// </summary>
    internal sealed class RemoteTreeView : TreeView
    {
        private sealed class NodeInfo
        {
            public string Path = string.Empty;
            public bool IsDrive;
            public bool IsPlaceholder;
            public bool Loaded;
            public bool Loading;
        }

        private RemoteBrowser browser;
        private bool suppress;              // true while the tree changes its own selection
        private int syncTicket;
        private bool editing;
        private readonly Dictionary<TreeNode, List<MethodInvoker>> waiting =
            new Dictionary<TreeNode, List<MethodInvoker>>();

        /// <summary>The user picked a folder (never raised for selections the tree makes itself).</summary>
        public event EventHandler<PathEventArgs> FolderPicked;
        public event EventHandler<TreeContextEventArgs> FolderContextMenu;
        public event EventHandler<TreeRenameEventArgs> RenameRequested;

        /// <summary>Asks whether files may be dropped on the folder under the cursor.</summary>
        public event EventHandler<DropQueryEventArgs> DropQuery;
        /// <summary>Files from Explorer were dropped on a folder.</summary>
        public event EventHandler<FilesDroppedEventArgs> FilesDropped;

        private TreeNode dropNode;

        public RemoteTreeView()
        {
            AllowDrop = true;
            HideSelection = false;
            ShowLines = false;
            ShowRootLines = true;
            LabelEdit = true;
            DoubleBuffered = true;
        }

        /// <summary>The path of the selected folder, or null.</summary>
        public string SelectedPath
        {
            get { return SelectedNode == null ? null : Info(SelectedNode).Path; }
        }

        /// <summary>True while a folder name is being edited.</summary>
        public bool IsEditing
        {
            get { return editing; }
        }

        // ---- setting up ----

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ShellIcons.ApplyExplorerTheme(Handle);
            ShellNative.SendMessage(Handle, ShellNative.TVM_SETIMAGELIST, new IntPtr(ShellNative.TVSIL_NORMAL),
                ShellIcons.SmallList);
        }

        /// <summary>Builds the root (the peer's name) and loads the drives under it.</summary>
        public void Initialize(RemoteBrowser remote)
        {
            browser = remote;
            waiting.Clear();
            syncTicket++;
            Nodes.Clear();

            TreeNode root = new TreeNode(remote.PeerName);
            NodeInfo info = new NodeInfo();
            info.Path = RemotePaths.Root;
            root.Tag = info;
            Nodes.Add(root);
            SetIcons(root, ShellIcons.ComputerIndex, ShellIcons.ComputerIndex);
            AddPlaceholder(root);

            LoadChildren(root, delegate
            {
                if (Info(root).Loaded)
                {
                    root.Expand();
                }
            });
        }

        // ---- loading ----

        private static NodeInfo Info(TreeNode node)
        {
            return (NodeInfo)node.Tag;
        }

        private void AddPlaceholder(TreeNode node)
        {
            TreeNode placeholder = new TreeNode(string.Empty);
            NodeInfo info = new NodeInfo();
            info.IsPlaceholder = true;
            placeholder.Tag = info;
            node.Nodes.Add(placeholder);
        }

        private void SetIcons(TreeNode node, int closed, int open)
        {
            if (!IsHandleCreated)
            {
                return;
            }
            ShellNative.TVITEM item = new ShellNative.TVITEM();
            item.mask = ShellNative.TVIF_HANDLE | ShellNative.TVIF_IMAGE | ShellNative.TVIF_SELECTEDIMAGE;
            item.hItem = node.Handle;
            item.iImage = closed;
            item.iSelectedImage = open;
            ShellNative.SendMessage(Handle, ShellNative.TVM_SETITEMW, IntPtr.Zero, ref item);
        }

        /// <summary>Loads a node's subfolders if that has not happened yet, then calls <paramref name="done"/>.</summary>
        private void LoadChildren(TreeNode node, MethodInvoker done)
        {
            NodeInfo info = Info(node);
            if (info.Loaded)
            {
                if (done != null)
                {
                    done();
                }
                return;
            }

            if (done != null)
            {
                List<MethodInvoker> list;
                if (!waiting.TryGetValue(node, out list))
                {
                    list = new List<MethodInvoker>();
                    waiting[node] = list;
                }
                list.Add(done);
            }
            if (info.Loading)
            {
                return;
            }
            info.Loading = true;

            browser.ListFolders(info.Path, delegate(List<RemoteItem> folders, Exception error)
            {
                info.Loading = false;
                if (node.TreeView == this)
                {
                    if (error == null)
                    {
                        Populate(node, folders);
                        info.Loaded = true;
                    }
                    else if (error is RemoteException)
                    {
                        // The folder cannot be listed (access denied, deleted, ...): it simply has no subfolders.
                        Log.Warn("Tree: cannot list '{0}': {1}", info.Path, error.Message);
                        node.Nodes.Clear();
                        info.Loaded = true;
                    }
                    else
                    {
                        Log.Warn("Tree: listing '{0}' failed: {1}", info.Path, error.Message);
                    }
                }

                List<MethodInvoker> callbacks;
                if (waiting.TryGetValue(node, out callbacks))
                {
                    waiting.Remove(node);
                    foreach (MethodInvoker callback in callbacks)
                    {
                        callback();
                    }
                }
            });
        }

        private void Populate(TreeNode node, List<RemoteItem> folders)
        {
            if (!RemotePaths.IsRoot(Info(node).Path))
            {
                folders.Sort(delegate(RemoteItem a, RemoteItem b)
                {
                    return ShellNative.StrCmpLogicalW(a.DisplayName, b.DisplayName);
                });
            }

            BeginUpdate();
            try
            {
                node.Nodes.Clear();
                foreach (RemoteItem folder in folders)
                {
                    TreeNode child = new TreeNode(folder.DisplayName);
                    NodeInfo info = new NodeInfo();
                    info.Path = folder.Path;
                    info.IsDrive = folder.IsDrive;
                    child.Tag = info;
                    node.Nodes.Add(child);
                    if (folder.IsDrive)
                    {
                        SetIcons(child, folder.IconIndex, folder.IconIndex);
                    }
                    else
                    {
                        SetIcons(child, ShellIcons.FolderIndex, ShellIcons.FolderOpenIndex);
                    }
                    AddPlaceholder(child);
                }
            }
            finally
            {
                EndUpdate();
            }
        }

        protected override void OnBeforeExpand(TreeViewCancelEventArgs e)
        {
            NodeInfo info = Info(e.Node);
            if (!info.Loaded && !info.IsPlaceholder)
            {
                // Load first, expand afterwards: the placeholder would otherwise flash as an empty folder.
                e.Cancel = true;
                TreeNode node = e.Node;
                LoadChildren(node, delegate
                {
                    if (node.TreeView == this && Info(node).Loaded)
                    {
                        node.Expand();
                    }
                });
                return;
            }
            base.OnBeforeExpand(e);
        }

        // ---- following the current folder ----

        /// <summary>
        /// Expands every level of the path (loading what is missing), then selects and shows its node. Selecting
        /// from here never raises <see cref="FolderPicked"/>.
        /// </summary>
        public void SyncTo(string path)
        {
            if (Nodes.Count == 0)
            {
                return;
            }
            int ticket = ++syncTicket;
            SyncStep(ticket, Nodes[0], RemotePaths.Chain(path), 0);
        }

        private void SyncStep(int ticket, TreeNode node, List<string> chain, int index)
        {
            if (ticket != syncTicket || node.TreeView != this)
            {
                return;
            }
            if (index == chain.Count)
            {
                SelectQuietly(node);
                return;
            }

            LoadChildren(node, delegate
            {
                if (ticket != syncTicket || node.TreeView != this)
                {
                    return;
                }
                TreeNode next = FindChild(node, chain[index]);
                if (next == null)
                {
                    SelectQuietly(node); // not in the tree (a protected folder, say): stop at the deepest level
                    return;
                }
                node.Expand();
                SyncStep(ticket, next, chain, index + 1);
            });
        }

        private void SelectQuietly(TreeNode node)
        {
            suppress = true;
            try
            {
                SelectedNode = node;
                node.EnsureVisible();
            }
            finally
            {
                suppress = false;
            }
        }

        private TreeNode FindChild(TreeNode parent, string path)
        {
            foreach (TreeNode child in parent.Nodes)
            {
                if (RemotePaths.SamePath(Info(child).Path, path))
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>The node for a folder, if the tree has loaded every level down to it.</summary>
        private TreeNode FindLoaded(string path)
        {
            if (Nodes.Count == 0)
            {
                return null;
            }
            TreeNode node = Nodes[0];
            foreach (string step in RemotePaths.Chain(path))
            {
                if (!Info(node).Loaded)
                {
                    return null;
                }
                node = FindChild(node, step);
                if (node == null)
                {
                    return null;
                }
            }
            return node;
        }

        // ---- refreshing ----

        /// <summary>Reloads every expanded folder, keeping the expansion wherever the folders still exist.</summary>
        public void RefreshAll(MethodInvoker done)
        {
            if (Nodes.Count > 0)
            {
                Reload(Nodes[0], done);
            }
            else if (done != null)
            {
                done();
            }
        }

        /// <summary>Reloads one folder's subfolders (after a rename, delete or new folder), if the tree shows it.</summary>
        public void RefreshFolder(string path, MethodInvoker done)
        {
            TreeNode node = FindLoaded(path);
            if (node != null)
            {
                Reload(node, done);
            }
            else if (done != null)
            {
                done();
            }
        }

        private void Reload(TreeNode node, MethodInvoker done)
        {
            List<string> expanded = new List<string>();
            CollectExpanded(node, expanded);
            bool wasExpanded = node.IsExpanded;

            Info(node).Loaded = false;
            node.Nodes.Clear();
            AddPlaceholder(node);
            LoadChildren(node, delegate
            {
                if (node.TreeView != this)
                {
                    return;
                }
                if (wasExpanded && Info(node).Loaded)
                {
                    node.Expand();
                }
                ReExpand(node, expanded, done);
            });
        }

        private void CollectExpanded(TreeNode node, List<string> result)
        {
            foreach (TreeNode child in node.Nodes)
            {
                if (child.IsExpanded)
                {
                    result.Add(Info(child).Path);
                    CollectExpanded(child, result);
                }
            }
        }

        private void ReExpand(TreeNode node, List<string> expanded, MethodInvoker done)
        {
            List<TreeNode> todo = new List<TreeNode>();
            foreach (TreeNode child in node.Nodes)
            {
                if (!Info(child).IsPlaceholder && expanded.Contains(Info(child).Path))
                {
                    todo.Add(child);
                }
            }
            ExpandNext(todo, 0, expanded, done);
        }

        private void ExpandNext(List<TreeNode> todo, int index, List<string> expanded, MethodInvoker done)
        {
            if (index >= todo.Count)
            {
                if (done != null)
                {
                    done();
                }
                return;
            }

            TreeNode child = todo[index];
            LoadChildren(child, delegate
            {
                if (child.TreeView != this)
                {
                    return;
                }
                if (Info(child).Loaded)
                {
                    child.Expand();
                }
                ReExpand(child, expanded, delegate { ExpandNext(todo, index + 1, expanded, done); });
            });
        }

        // ---- drag and drop from Explorer ----

        private void SetDropNode(TreeNode node)
        {
            if (node == dropNode)
            {
                return;
            }
            dropNode = node;
            ShellNative.SendMessage(Handle, ShellNative.TVM_SELECTITEM, new IntPtr(ShellNative.TVGN_DROPHILITE),
                node == null ? IntPtr.Zero : node.Handle);
        }

        /// <summary>The folder node under the cursor that accepts the drop, or null.</summary>
        private TreeNode UpdateDrag(DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            TreeNode target = null;
            if (DragHelper.HasFiles(e) && (e.AllowedEffect & DragDropEffects.Copy) != 0)
            {
                Point client = PointToClient(new Point(e.X, e.Y));
                DragHelper.AutoScroll(this, client);
                TreeNode node = GetNodeAt(client);
                if (node != null && !Info(node).IsPlaceholder)
                {
                    DropQueryEventArgs query = new DropQueryEventArgs(Info(node).Path);
                    EventHandler<DropQueryEventArgs> handler = DropQuery;
                    if (handler != null)
                    {
                        handler(this, query);
                    }
                    if (query.Allowed)
                    {
                        e.Effect = DragDropEffects.Copy;
                        target = node;
                    }
                }
            }
            SetDropNode(target);
            return target;
        }

        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            base.OnDragEnter(drgevent);
            UpdateDrag(drgevent);
        }

        protected override void OnDragOver(DragEventArgs drgevent)
        {
            base.OnDragOver(drgevent);
            UpdateDrag(drgevent);
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            SetDropNode(null);
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);
            TreeNode target = UpdateDrag(drgevent);
            SetDropNode(null);
            string[] files = DragHelper.GetFiles(drgevent);
            EventHandler<FilesDroppedEventArgs> handler = FilesDropped;
            if (target != null && files != null && files.Length > 0 && handler != null)
            {
                handler(this, new FilesDroppedEventArgs(files, Info(target).Path));
            }
        }

        // ---- user input ----

        protected override void OnAfterSelect(TreeViewEventArgs e)
        {
            base.OnAfterSelect(e);
            if (suppress || e.Action == TreeViewAction.Unknown)
            {
                return; // the tree moved the selection itself (a sync, or a collapse): not a navigation
            }
            NodeInfo info = Info(e.Node);
            EventHandler<PathEventArgs> handler = FolderPicked;
            if (handler != null && !info.IsPlaceholder)
            {
                handler(this, new PathEventArgs(info.Path));
            }
        }

        protected override void OnNodeMouseClick(TreeNodeMouseClickEventArgs e)
        {
            base.OnNodeMouseClick(e);
            if (e.Button == MouseButtons.Right && !Info(e.Node).IsPlaceholder)
            {
                EventHandler<TreeContextEventArgs> handler = FolderContextMenu;
                if (handler != null)
                {
                    NodeInfo info = Info(e.Node);
                    handler(this, new TreeContextEventArgs(info.Path, info.IsDrive || RemotePaths.IsRoot(info.Path),
                        e.Location));
                }
            }
        }

        /// <summary>Starts editing the name of the node for a folder, if the tree shows it.</summary>
        public void BeginRename(string path)
        {
            TreeNode node = FindLoaded(path);
            if (node != null)
            {
                node.BeginEdit();
            }
        }

        protected override void OnBeforeLabelEdit(NodeLabelEditEventArgs e)
        {
            NodeInfo info = Info(e.Node);
            if (info.IsDrive || info.IsPlaceholder || RemotePaths.IsRoot(info.Path))
            {
                e.CancelEdit = true;
                return;
            }
            editing = true;
            base.OnBeforeLabelEdit(e);
        }

        protected override void OnAfterLabelEdit(NodeLabelEditEventArgs e)
        {
            editing = false;
            string path = Info(e.Node).Path;
            string newName = e.Label;
            e.CancelEdit = true; // the tree is reloaded from the peer's answer
            base.OnAfterLabelEdit(e);

            if (newName != null && newName != RemotePaths.GetName(path))
            {
                EventHandler<TreeRenameEventArgs> handler = RenameRequested;
                if (handler != null)
                {
                    handler(this, new TreeRenameEventArgs(path, newName));
                }
            }
        }
    }
}
