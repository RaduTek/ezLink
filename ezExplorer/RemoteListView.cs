using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    internal sealed class RenameEventArgs : EventArgs
    {
        public readonly RemoteItem Item;
        public readonly string NewName;

        public RenameEventArgs(RemoteItem item, string newName)
        {
            Item = item;
            NewName = newName;
        }
    }

    /// <summary>
    /// The right-hand pane: a virtual-mode ListView over the remote folder's entries, with the shell's own icons
    /// and type names, folders-first sorting and Explorer-style keys. Virtual mode keeps a folder of tens of
    /// thousands of files cheap: only the rows on screen are ever turned into list view items.
    /// </summary>
    internal sealed class RemoteListView : ListView
    {
        private readonly ColumnHeader nameColumn = new ColumnHeader();
        private readonly ColumnHeader sizeColumn = new ColumnHeader();
        private readonly ColumnHeader typeColumn = new ColumnHeader();
        private readonly ColumnHeader dateColumn = new ColumnHeader();

        private List<RemoteItem> items = new List<RemoteItem>();
        private int sortColumn;
        private bool sortAscending = true;
        private bool editing;

        /// <summary>The selection changed (also raised for range selections in virtual mode).</summary>
        public event EventHandler SelectionChanged;
        /// <summary>The user finished editing a name (the edit itself is already cancelled; do the rename).</summary>
        public event EventHandler<RenameEventArgs> RenameRequested;

        /// <summary>Asks whether files may be dropped on the folder under the cursor.</summary>
        public event EventHandler<DropQueryEventArgs> DropQuery;
        /// <summary>Files from Explorer were dropped.</summary>
        public event EventHandler<FilesDroppedEventArgs> FilesDropped;

        private int dropHilite = -1;

        public RemoteListView()
        {
            AllowDrop = true;
            DoubleBuffered = true;
            View = View.Details;
            VirtualMode = true;
            FullRowSelect = true;
            HideSelection = false;
            MultiSelect = true;
            LabelEdit = true;

            nameColumn.Text = Strings.Get("ColName");
            nameColumn.Width = 260;
            sizeColumn.Text = Strings.Get("ColSize");
            sizeColumn.Width = 90;
            sizeColumn.TextAlign = HorizontalAlignment.Right;
            typeColumn.Text = Strings.Get("ColType");
            typeColumn.Width = 140;
            dateColumn.Text = Strings.Get("ColModified");
            dateColumn.Width = 140;
            Columns.AddRange(new ColumnHeader[] { nameColumn, sizeColumn, typeColumn, dateColumn });
        }

        public int ItemCount
        {
            get { return items.Count; }
        }

        /// <summary>True while a name is being edited, so keys such as Backspace belong to the edit box.</summary>
        public bool IsEditing
        {
            get { return editing; }
        }

        public RemoteItem ItemAt(int index)
        {
            return items[index];
        }

        public List<RemoteItem> SelectedRemoteItems
        {
            get
            {
                List<RemoteItem> selected = new List<RemoteItem>(SelectedIndices.Count);
                foreach (int index in SelectedIndices)
                {
                    selected.Add(items[index]);
                }
                return selected;
            }
        }

        // ---- contents ----

        public void SetItems(List<RemoteItem> list)
        {
            items = list;
            SortItems();
            VirtualListSize = 0;            // also clears the selection
            VirtualListSize = items.Count;
            Invalidate();                   // setting the size does not repaint rows that were already shown
            if (items.Count > 0)
            {
                EnsureVisible(0);
            }
            RaiseSelectionChanged();
        }

        public int IndexOfName(string name)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        public void SelectOnly(int index)
        {
            SelectedIndices.Clear();
            if (index >= 0 && index < items.Count)
            {
                SelectedIndices.Add(index);
                FocusIndex(index);
            }
        }

        public void SelectAllItems()
        {
            // One message selects every row (item -1 means "all"); adding the indices one by one would raise a
            // selection event for each of tens of thousands of rows.
            ShellNative.LVITEM state = new ShellNative.LVITEM();
            state.state = ShellNative.LVIS_SELECTED;
            state.stateMask = ShellNative.LVIS_SELECTED;
            ShellNative.SendMessage(Handle, ShellNative.LVM_SETITEMSTATE, new IntPtr(-1), ref state);
        }

        public void BeginRename(int index)
        {
            if (index >= 0 && index < items.Count && !items[index].IsDrive)
            {
                FocusIndex(index);
                Items[index].BeginEdit();
            }
        }

        private void FocusIndex(int index)
        {
            EnsureVisible(index);
            Items[index].Focused = true;
        }

        // ---- the view ----

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ShellIcons.ApplyExplorerTheme(Handle);
            // The system image lists are shared: attach them natively instead of copying them into ImageLists.
            ShellNative.SendMessage(Handle, ShellNative.LVM_SETIMAGELIST, new IntPtr(ShellNative.LVSIL_SMALL),
                ShellIcons.SmallList);
            ShellNative.SendMessage(Handle, ShellNative.LVM_SETIMAGELIST, new IntPtr(ShellNative.LVSIL_NORMAL),
                ShellIcons.LargeList);
        }

        protected override void OnRetrieveVirtualItem(RetrieveVirtualItemEventArgs e)
        {
            RemoteItem item = items[e.ItemIndex];
            ListViewItem row = new ListViewItem(item.DisplayName, item.IconIndex);
            row.SubItems.Add(item.IsFolder ? string.Empty : FormatSize(item.Size));
            row.SubItems.Add(item.TypeName);
            row.SubItems.Add(item.HasDate ? item.Modified.ToString("g") : string.Empty);
            e.Item = row;
        }

        protected override void OnSearchForVirtualItem(SearchForVirtualItemEventArgs e)
        {
            // Type-to-select.
            if (e.IsTextSearch && items.Count > 0)
            {
                int start = Math.Max(0, e.StartIndex);
                for (int n = 0; n < items.Count; n++)
                {
                    int index = (start + n) % items.Count;
                    if (items[index].DisplayName.StartsWith(e.Text, StringComparison.CurrentCultureIgnoreCase))
                    {
                        e.Index = index;
                        return;
                    }
                }
            }
            base.OnSearchForVirtualItem(e);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            RaiseSelectionChanged();
        }

        protected override void OnVirtualItemsSelectionRangeChanged(ListViewVirtualItemsSelectionRangeChangedEventArgs e)
        {
            base.OnVirtualItemsSelectionRangeChanged(e);
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>Explorer shows sizes in whole kilobytes, rounded up.</summary>
        public static string FormatSize(long bytes)
        {
            return ((bytes + 1023) / 1024).ToString("N0") + " KB";
        }

        // ---- renaming ----

        protected override void OnBeforeLabelEdit(LabelEditEventArgs e)
        {
            if (items[e.Item].IsDrive)
            {
                e.CancelEdit = true;
                return;
            }
            editing = true;
            base.OnBeforeLabelEdit(e);
        }

        protected override void OnAfterLabelEdit(LabelEditEventArgs e)
        {
            editing = false;
            RemoteItem item = items[e.Item];
            string newName = e.Label;
            e.CancelEdit = true; // the list is rebuilt from the peer's answer, never from the edit box
            base.OnAfterLabelEdit(e);

            if (newName != null && newName != item.Name)
            {
                EventHandler<RenameEventArgs> handler = RenameRequested;
                if (handler != null)
                {
                    handler(this, new RenameEventArgs(item, newName));
                }
            }
        }

        // ---- drag and drop from Explorer ----

        /// <summary>The index of the folder row under a client point, or -1.</summary>
        private int FolderIndexAt(Point client)
        {
            ListViewItem hit = HitTest(client).Item;
            if (hit != null && hit.Index >= 0 && hit.Index < items.Count && items[hit.Index].IsFolder)
            {
                return hit.Index;
            }
            return -1;
        }

        private void SetDropHilite(int index)
        {
            if (index == dropHilite)
            {
                return;
            }
            if (dropHilite >= 0)
            {
                SetItemDropState(dropHilite, 0);
            }
            dropHilite = index;
            if (index >= 0)
            {
                SetItemDropState(index, ShellNative.LVIS_DROPHILITED);
            }
        }

        private void SetItemDropState(int index, uint state)
        {
            ShellNative.LVITEM item = new ShellNative.LVITEM();
            item.state = state;
            item.stateMask = ShellNative.LVIS_DROPHILITED;
            ShellNative.SendMessage(Handle, ShellNative.LVM_SETITEMSTATE, new IntPtr(index), ref item);
        }

        private void UpdateDrag(DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            int index = -1;
            if (DragHelper.HasFiles(e) && (e.AllowedEffect & DragDropEffects.Copy) != 0)
            {
                Point client = PointToClient(new Point(e.X, e.Y));
                DragHelper.AutoScroll(this, client);
                index = FolderIndexAt(client);

                DropQueryEventArgs query = new DropQueryEventArgs(index >= 0 ? items[index].Path : null);
                EventHandler<DropQueryEventArgs> handler = DropQuery;
                if (handler != null)
                {
                    handler(this, query);
                }
                if (query.Allowed)
                {
                    e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    index = -1;
                }
            }
            SetDropHilite(index);
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
            SetDropHilite(-1);
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);
            UpdateDrag(drgevent);
            bool allowed = drgevent.Effect != DragDropEffects.None;
            int index = dropHilite;
            SetDropHilite(-1);
            if (!allowed)
            {
                return;
            }

            string[] files = DragHelper.GetFiles(drgevent);
            EventHandler<FilesDroppedEventArgs> handler = FilesDropped;
            if (files != null && files.Length > 0 && handler != null)
            {
                handler(this, new FilesDroppedEventArgs(files, index >= 0 ? items[index].Path : null));
            }
        }

        // ---- sorting ----

        protected override void OnColumnClick(ColumnClickEventArgs e)
        {
            base.OnColumnClick(e);
            if (e.Column == sortColumn)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                sortColumn = e.Column;
                sortAscending = true;
            }

            // Keep the same rows selected after the order changes.
            List<string> selectedPaths = new List<string>();
            foreach (RemoteItem item in SelectedRemoteItems)
            {
                selectedPaths.Add(item.Path);
            }
            SortItems();
            SelectedIndices.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                if (selectedPaths.Count > 0 && selectedPaths.Contains(items[i].Path))
                {
                    SelectedIndices.Add(i);
                }
            }
            Invalidate();
        }

        private void SortItems()
        {
            items.Sort(new Comparison<RemoteItem>(Compare));
        }

        private int Compare(RemoteItem a, RemoteItem b)
        {
            if (a.IsFolder != b.IsFolder)
            {
                return a.IsFolder ? -1 : 1; // folders stay on top whichever way the column sorts
            }

            int result;
            switch (sortColumn)
            {
                case 1:
                    result = a.Size.CompareTo(b.Size);
                    break;
                case 2:
                    result = string.Compare(a.TypeName, b.TypeName, StringComparison.CurrentCultureIgnoreCase);
                    break;
                case 3:
                    result = a.Modified.CompareTo(b.Modified);
                    break;
                default:
                    result = ShellNative.StrCmpLogicalW(a.DisplayName, b.DisplayName);
                    break;
            }
            if (!sortAscending)
            {
                result = -result;
            }
            if (result == 0)
            {
                result = ShellNative.StrCmpLogicalW(a.DisplayName, b.DisplayName);
            }
            return result;
        }
    }
}
