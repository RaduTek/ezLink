namespace ezExplorer
{
    partial class ExplorerForm
    {
        /// <summary>Required designer variable.</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>Clean up any resources being used.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>Required method for Designer support - do not modify the contents of this method with the code editor.</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolStrip = new ezExplorer.NativeToolStrip.NativeToolStrip();
            this.backButton = new System.Windows.Forms.ToolStripButton();
            this.forwardButton = new System.Windows.Forms.ToolStripButton();
            this.upButton = new System.Windows.Forms.ToolStripButton();
            this.toolSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.refreshButton = new System.Windows.Forms.ToolStripButton();
            this.toolSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.viewsButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.detailsItem = new System.Windows.Forms.ToolStripMenuItem();
            this.iconsItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pathPanel = new System.Windows.Forms.Panel();
            this.pathBox = new System.Windows.Forms.TextBox();
            this.split = new System.Windows.Forms.SplitContainer();
            this.tree = new ezExplorer.RemoteTreeView();
            this.list = new ezExplorer.RemoteListView();
            this.listMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.listOpenItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.listCopyItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listPasteItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.listRenameItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listDeleteItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.listNewFolderItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listRefreshItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.selectionLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.connectionLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.treeMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.treeOpenItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.treeCopyItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treePasteItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.treeRenameItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeDeleteItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.treeNewFolderItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeRefreshItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip.SuspendLayout();
            this.pathPanel.SuspendLayout();
            this.split.Panel1.SuspendLayout();
            this.split.Panel2.SuspendLayout();
            this.split.SuspendLayout();
            this.listMenu.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.treeMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip
            // 
            this.toolStrip.AutoSize = false;
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(22, 22);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.backButton,
            this.forwardButton,
            this.upButton,
            this.toolSeparator1,
            this.refreshButton,
            this.toolSeparator2,
            this.viewsButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Padding = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.toolStrip.Size = new System.Drawing.Size(800, 30);
            this.toolStrip.TabIndex = 2;
            this.toolStrip.Theme = ezExplorer.NativeToolStrip.NativeToolStripTheme.Toolbar;
            // 
            // backButton
            // 
            this.backButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.backButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.backButton.Name = "backButton";
            this.backButton.Size = new System.Drawing.Size(23, 27);
            this.backButton.Text = "Back";
            this.backButton.Click += new System.EventHandler(this.backButton_Click);
            // 
            // forwardButton
            // 
            this.forwardButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.forwardButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.forwardButton.Name = "forwardButton";
            this.forwardButton.Size = new System.Drawing.Size(23, 27);
            this.forwardButton.Text = "Forward";
            this.forwardButton.Click += new System.EventHandler(this.forwardButton_Click);
            // 
            // upButton
            // 
            this.upButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.upButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.upButton.Name = "upButton";
            this.upButton.Size = new System.Drawing.Size(23, 27);
            this.upButton.Text = "Up";
            this.upButton.Click += new System.EventHandler(this.upButton_Click);
            // 
            // toolSeparator1
            // 
            this.toolSeparator1.Name = "toolSeparator1";
            this.toolSeparator1.Size = new System.Drawing.Size(6, 30);
            // 
            // refreshButton
            // 
            this.refreshButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.refreshButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(23, 27);
            this.refreshButton.Text = "Refresh";
            this.refreshButton.Click += new System.EventHandler(this.OnRefreshClick);
            // 
            // toolSeparator2
            // 
            this.toolSeparator2.Name = "toolSeparator2";
            this.toolSeparator2.Size = new System.Drawing.Size(6, 30);
            // 
            // viewsButton
            // 
            this.viewsButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.detailsItem,
            this.iconsItem,
            this.listItem});
            this.viewsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.viewsButton.Name = "viewsButton";
            this.viewsButton.Size = new System.Drawing.Size(50, 27);
            this.viewsButton.Text = "Views";
            // 
            // detailsItem
            // 
            this.detailsItem.Name = "detailsItem";
            this.detailsItem.Size = new System.Drawing.Size(134, 22);
            this.detailsItem.Text = "Details";
            this.detailsItem.Click += new System.EventHandler(this.detailsItem_Click);
            // 
            // iconsItem
            // 
            this.iconsItem.Name = "iconsItem";
            this.iconsItem.Size = new System.Drawing.Size(134, 22);
            this.iconsItem.Text = "Large icons";
            this.iconsItem.Click += new System.EventHandler(this.iconsItem_Click);
            // 
            // listItem
            // 
            this.listItem.Name = "listItem";
            this.listItem.Size = new System.Drawing.Size(134, 22);
            this.listItem.Text = "List";
            this.listItem.Click += new System.EventHandler(this.listItem_Click);
            // 
            // pathPanel
            // 
            this.pathPanel.Controls.Add(this.pathBox);
            this.pathPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.pathPanel.Location = new System.Drawing.Point(0, 30);
            this.pathPanel.Name = "pathPanel";
            this.pathPanel.Padding = new System.Windows.Forms.Padding(4);
            this.pathPanel.Size = new System.Drawing.Size(800, 31);
            this.pathPanel.TabIndex = 1;
            // 
            // pathBox
            // 
            this.pathBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pathBox.Location = new System.Drawing.Point(4, 4);
            this.pathBox.Name = "pathBox";
            this.pathBox.Size = new System.Drawing.Size(792, 23);
            this.pathBox.TabIndex = 0;
            this.pathBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnPathKeyDown);
            // 
            // split
            // 
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.split.Location = new System.Drawing.Point(0, 61);
            this.split.Name = "split";
            // 
            // split.Panel1
            // 
            this.split.Panel1.Controls.Add(this.tree);
            // 
            // split.Panel2
            // 
            this.split.Panel2.Controls.Add(this.list);
            this.split.Size = new System.Drawing.Size(800, 417);
            this.split.SplitterDistance = 200;
            this.split.TabIndex = 0;
            // 
            // tree
            // 
            this.tree.AllowDrop = true;
            this.tree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tree.HideSelection = false;
            this.tree.LabelEdit = true;
            this.tree.Location = new System.Drawing.Point(0, 0);
            this.tree.Name = "tree";
            this.tree.ShowLines = false;
            this.tree.Size = new System.Drawing.Size(200, 417);
            this.tree.TabIndex = 1;
            // 
            // list
            // 
            this.list.AllowDrop = true;
            this.list.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.list.ContextMenuStrip = this.listMenu;
            this.list.Dock = System.Windows.Forms.DockStyle.Fill;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.LabelEdit = true;
            this.list.Location = new System.Drawing.Point(0, 0);
            this.list.Name = "list";
            this.list.Size = new System.Drawing.Size(596, 417);
            this.list.TabIndex = 2;
            this.list.UseCompatibleStateImageBehavior = false;
            this.list.View = System.Windows.Forms.View.Details;
            this.list.VirtualMode = true;
            // 
            // listMenu
            // 
            this.listMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.listOpenItem,
            this.listSeparator1,
            this.listCopyItem,
            this.listPasteItem,
            this.listSeparator2,
            this.listRenameItem,
            this.listDeleteItem,
            this.listSeparator3,
            this.listNewFolderItem,
            this.listRefreshItem});
            this.listMenu.Name = "listMenu";
            this.listMenu.Size = new System.Drawing.Size(133, 176);
            this.listMenu.Opening += new System.ComponentModel.CancelEventHandler(this.OnListMenuOpening);
            // 
            // listOpenItem
            // 
            this.listOpenItem.Name = "listOpenItem";
            this.listOpenItem.Size = new System.Drawing.Size(132, 22);
            this.listOpenItem.Text = "Open";
            this.listOpenItem.Click += new System.EventHandler(this.OnListOpen);
            // 
            // listSeparator1
            // 
            this.listSeparator1.Name = "listSeparator1";
            this.listSeparator1.Size = new System.Drawing.Size(129, 6);
            // 
            // listCopyItem
            // 
            this.listCopyItem.Name = "listCopyItem";
            this.listCopyItem.Size = new System.Drawing.Size(132, 22);
            this.listCopyItem.Text = "Copy";
            this.listCopyItem.Click += new System.EventHandler(this.OnListCopy);
            // 
            // listPasteItem
            // 
            this.listPasteItem.Name = "listPasteItem";
            this.listPasteItem.Size = new System.Drawing.Size(132, 22);
            this.listPasteItem.Text = "Paste";
            this.listPasteItem.Click += new System.EventHandler(this.OnListPaste);
            // 
            // listSeparator2
            // 
            this.listSeparator2.Name = "listSeparator2";
            this.listSeparator2.Size = new System.Drawing.Size(129, 6);
            // 
            // listRenameItem
            // 
            this.listRenameItem.Name = "listRenameItem";
            this.listRenameItem.Size = new System.Drawing.Size(132, 22);
            this.listRenameItem.Text = "Rename";
            this.listRenameItem.Click += new System.EventHandler(this.OnListRename);
            // 
            // listDeleteItem
            // 
            this.listDeleteItem.Name = "listDeleteItem";
            this.listDeleteItem.Size = new System.Drawing.Size(132, 22);
            this.listDeleteItem.Text = "Delete";
            this.listDeleteItem.Click += new System.EventHandler(this.OnListDelete);
            // 
            // listSeparator3
            // 
            this.listSeparator3.Name = "listSeparator3";
            this.listSeparator3.Size = new System.Drawing.Size(129, 6);
            // 
            // listNewFolderItem
            // 
            this.listNewFolderItem.Name = "listNewFolderItem";
            this.listNewFolderItem.Size = new System.Drawing.Size(132, 22);
            this.listNewFolderItem.Text = "New folder";
            this.listNewFolderItem.Click += new System.EventHandler(this.OnListNewFolder);
            // 
            // listRefreshItem
            // 
            this.listRefreshItem.Name = "listRefreshItem";
            this.listRefreshItem.Size = new System.Drawing.Size(132, 22);
            this.listRefreshItem.Text = "Refresh";
            this.listRefreshItem.Click += new System.EventHandler(this.OnRefreshClick);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectionLabel,
            this.connectionLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 478);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(800, 22);
            this.statusStrip.TabIndex = 3;
            // 
            // selectionLabel
            // 
            this.selectionLabel.Name = "selectionLabel";
            this.selectionLabel.Size = new System.Drawing.Size(785, 17);
            this.selectionLabel.Spring = true;
            this.selectionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // connectionLabel
            // 
            this.connectionLabel.Name = "connectionLabel";
            this.connectionLabel.Size = new System.Drawing.Size(0, 17);
            this.connectionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // treeMenu
            // 
            this.treeMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.treeOpenItem,
            this.treeSeparator1,
            this.treeCopyItem,
            this.treePasteItem,
            this.treeSeparator2,
            this.treeRenameItem,
            this.treeDeleteItem,
            this.treeSeparator3,
            this.treeNewFolderItem,
            this.treeRefreshItem});
            this.treeMenu.Name = "treeMenu";
            this.treeMenu.Size = new System.Drawing.Size(133, 176);
            // 
            // treeOpenItem
            // 
            this.treeOpenItem.Name = "treeOpenItem";
            this.treeOpenItem.Size = new System.Drawing.Size(132, 22);
            this.treeOpenItem.Text = "Open";
            this.treeOpenItem.Click += new System.EventHandler(this.OnTreeOpen);
            // 
            // treeSeparator1
            // 
            this.treeSeparator1.Name = "treeSeparator1";
            this.treeSeparator1.Size = new System.Drawing.Size(129, 6);
            // 
            // treeCopyItem
            // 
            this.treeCopyItem.Name = "treeCopyItem";
            this.treeCopyItem.Size = new System.Drawing.Size(132, 22);
            this.treeCopyItem.Text = "Copy";
            this.treeCopyItem.Click += new System.EventHandler(this.OnTreeCopy);
            // 
            // treePasteItem
            // 
            this.treePasteItem.Name = "treePasteItem";
            this.treePasteItem.Size = new System.Drawing.Size(132, 22);
            this.treePasteItem.Text = "Paste";
            this.treePasteItem.Click += new System.EventHandler(this.OnTreePaste);
            // 
            // treeSeparator2
            // 
            this.treeSeparator2.Name = "treeSeparator2";
            this.treeSeparator2.Size = new System.Drawing.Size(129, 6);
            // 
            // treeRenameItem
            // 
            this.treeRenameItem.Name = "treeRenameItem";
            this.treeRenameItem.Size = new System.Drawing.Size(132, 22);
            this.treeRenameItem.Text = "Rename";
            this.treeRenameItem.Click += new System.EventHandler(this.OnTreeMenuRename);
            // 
            // treeDeleteItem
            // 
            this.treeDeleteItem.Name = "treeDeleteItem";
            this.treeDeleteItem.Size = new System.Drawing.Size(132, 22);
            this.treeDeleteItem.Text = "Delete";
            this.treeDeleteItem.Click += new System.EventHandler(this.OnTreeDelete);
            // 
            // treeSeparator3
            // 
            this.treeSeparator3.Name = "treeSeparator3";
            this.treeSeparator3.Size = new System.Drawing.Size(129, 6);
            // 
            // treeNewFolderItem
            // 
            this.treeNewFolderItem.Name = "treeNewFolderItem";
            this.treeNewFolderItem.Size = new System.Drawing.Size(132, 22);
            this.treeNewFolderItem.Text = "New folder";
            this.treeNewFolderItem.Click += new System.EventHandler(this.OnTreeNewFolder);
            // 
            // treeRefreshItem
            // 
            this.treeRefreshItem.Name = "treeRefreshItem";
            this.treeRefreshItem.Size = new System.Drawing.Size(132, 22);
            this.treeRefreshItem.Text = "Refresh";
            this.treeRefreshItem.Click += new System.EventHandler(this.OnRefreshClick);
            // 
            // ExplorerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(800, 500);
            this.Controls.Add(this.split);
            this.Controls.Add(this.pathPanel);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(480, 320);
            this.Name = "ExplorerForm";
            this.Text = "ExplorerForm";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.pathPanel.ResumeLayout(false);
            this.pathPanel.PerformLayout();
            this.split.Panel1.ResumeLayout(false);
            this.split.Panel2.ResumeLayout(false);
            this.split.ResumeLayout(false);
            this.listMenu.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.treeMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ezExplorer.NativeToolStrip.NativeToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton backButton;
        private System.Windows.Forms.ToolStripButton forwardButton;
        private System.Windows.Forms.ToolStripButton upButton;
        private System.Windows.Forms.ToolStripSeparator toolSeparator1;
        private System.Windows.Forms.ToolStripButton refreshButton;
        private System.Windows.Forms.ToolStripSeparator toolSeparator2;
        private System.Windows.Forms.ToolStripDropDownButton viewsButton;
        private System.Windows.Forms.ToolStripMenuItem detailsItem;
        private System.Windows.Forms.ToolStripMenuItem iconsItem;
        private System.Windows.Forms.ToolStripMenuItem listItem;
        private System.Windows.Forms.Panel pathPanel;
        private System.Windows.Forms.TextBox pathBox;
        private System.Windows.Forms.SplitContainer split;
        private ezExplorer.RemoteTreeView tree;
        private ezExplorer.RemoteListView list;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel selectionLabel;
        private System.Windows.Forms.ToolStripStatusLabel connectionLabel;
        private System.Windows.Forms.ContextMenuStrip listMenu;
        private System.Windows.Forms.ToolStripMenuItem listOpenItem;
        private System.Windows.Forms.ToolStripSeparator listSeparator1;
        private System.Windows.Forms.ToolStripMenuItem listCopyItem;
        private System.Windows.Forms.ToolStripMenuItem listPasteItem;
        private System.Windows.Forms.ToolStripSeparator listSeparator2;
        private System.Windows.Forms.ToolStripMenuItem listRenameItem;
        private System.Windows.Forms.ToolStripMenuItem listDeleteItem;
        private System.Windows.Forms.ToolStripSeparator listSeparator3;
        private System.Windows.Forms.ToolStripMenuItem listNewFolderItem;
        private System.Windows.Forms.ToolStripMenuItem listRefreshItem;
        private System.Windows.Forms.ContextMenuStrip treeMenu;
        private System.Windows.Forms.ToolStripMenuItem treeOpenItem;
        private System.Windows.Forms.ToolStripSeparator treeSeparator1;
        private System.Windows.Forms.ToolStripMenuItem treeCopyItem;
        private System.Windows.Forms.ToolStripMenuItem treePasteItem;
        private System.Windows.Forms.ToolStripSeparator treeSeparator2;
        private System.Windows.Forms.ToolStripMenuItem treeRenameItem;
        private System.Windows.Forms.ToolStripMenuItem treeDeleteItem;
        private System.Windows.Forms.ToolStripSeparator treeSeparator3;
        private System.Windows.Forms.ToolStripMenuItem treeNewFolderItem;
        private System.Windows.Forms.ToolStripMenuItem treeRefreshItem;
    }
}
