namespace ezLink
{
    partial class OptionsForm
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
            this.startAtLogon = new System.Windows.Forms.CheckBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.tabs = new System.Windows.Forms.TabControl();
            this.generalTab = new System.Windows.Forms.TabPage();
            this.connectionActionsGroup = new System.Windows.Forms.GroupBox();
            this.showWindowOnConnect = new System.Windows.Forms.CheckBox();
            this.startSyncOnConnect = new System.Windows.Forms.CheckBox();
            this.openExplorerOnConnect = new System.Windows.Forms.CheckBox();
            this.securityTab = new System.Windows.Forms.TabPage();
            this.fileTransferGroup = new System.Windows.Forms.GroupBox();
            this.protectSystem = new System.Windows.Forms.CheckBox();
            this.showHidden = new System.Windows.Forms.CheckBox();
            this.browseCopy = new System.Windows.Forms.CheckBox();
            this.write = new System.Windows.Forms.CheckBox();
            this.allowAll = new System.Windows.Forms.CheckBox();
            this.trustedDevicesTab = new System.Windows.Forms.TabPage();
            this.trustedHint = new System.Windows.Forms.Label();
            this.trustedList = new System.Windows.Forms.ListView();
            this.nameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.accessColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lastSeenColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.editButton = new System.Windows.Forms.Button();
            this.removeButton = new System.Windows.Forms.Button();
            this.fileSharingTab = new System.Windows.Forms.TabPage();
            this.shareProfile = new System.Windows.Forms.CheckBox();
            this.shareFixed = new System.Windows.Forms.CheckBox();
            this.shareRemovable = new System.Windows.Forms.CheckBox();
            this.fixedReadOnly = new System.Windows.Forms.CheckBox();
            this.removableReadOnly = new System.Windows.Forms.CheckBox();
            this.sharedFoldersLabel = new System.Windows.Forms.Label();
            this.sharedList = new System.Windows.Forms.ListView();
            this.sharedNameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.sharedPathColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.sharedReadOnlyColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.addFolderButton = new System.Windows.Forms.Button();
            this.addDriveButton = new System.Windows.Forms.Button();
            this.toggleReadOnlyButton = new System.Windows.Forms.Button();
            this.removeShareButton = new System.Windows.Forms.Button();
            this.informationTab = new System.Windows.Forms.TabPage();
            this.computerNameLabel = new System.Windows.Forms.Label();
            this.computerNameBox = new System.Windows.Forms.TextBox();
            this.computerIdLabel = new System.Windows.Forms.Label();
            this.computerIdBox = new System.Windows.Forms.TextBox();
            this.tabs.SuspendLayout();
            this.generalTab.SuspendLayout();
            this.connectionActionsGroup.SuspendLayout();
            this.securityTab.SuspendLayout();
            this.fileTransferGroup.SuspendLayout();
            this.trustedDevicesTab.SuspendLayout();
            this.fileSharingTab.SuspendLayout();
            this.informationTab.SuspendLayout();
            this.SuspendLayout();
            // 
            // startAtLogon
            // 
            this.startAtLogon.AutoSize = true;
            this.startAtLogon.Location = new System.Drawing.Point(11, 14);
            this.startAtLogon.Name = "startAtLogon";
            this.startAtLogon.Size = new System.Drawing.Size(89, 17);
            this.startAtLogon.TabIndex = 0;
            this.startAtLogon.Text = "Start at logon";
            this.startAtLogon.UseVisualStyleBackColor = true;
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(267, 269);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 6;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(348, 269);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 7;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // tabs
            // 
            this.tabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabs.Controls.Add(this.generalTab);
            this.tabs.Controls.Add(this.securityTab);
            this.tabs.Controls.Add(this.trustedDevicesTab);
            this.tabs.Controls.Add(this.fileSharingTab);
            this.tabs.Controls.Add(this.informationTab);
            this.tabs.Location = new System.Drawing.Point(9, 8);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(417, 257);
            this.tabs.TabIndex = 8;
            // 
            // generalTab
            // 
            this.generalTab.Controls.Add(this.connectionActionsGroup);
            this.generalTab.Controls.Add(this.startAtLogon);
            this.generalTab.Location = new System.Drawing.Point(4, 22);
            this.generalTab.Name = "generalTab";
            this.generalTab.Padding = new System.Windows.Forms.Padding(3);
            this.generalTab.Size = new System.Drawing.Size(409, 231);
            this.generalTab.TabIndex = 0;
            this.generalTab.Text = "General";
            this.generalTab.UseVisualStyleBackColor = true;
            // 
            // connectionActionsGroup
            // 
            this.connectionActionsGroup.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.connectionActionsGroup.Controls.Add(this.showWindowOnConnect);
            this.connectionActionsGroup.Controls.Add(this.startSyncOnConnect);
            this.connectionActionsGroup.Controls.Add(this.openExplorerOnConnect);
            this.connectionActionsGroup.Location = new System.Drawing.Point(11, 37);
            this.connectionActionsGroup.Name = "connectionActionsGroup";
            this.connectionActionsGroup.Size = new System.Drawing.Size(384, 100);
            this.connectionActionsGroup.TabIndex = 4;
            this.connectionActionsGroup.TabStop = false;
            this.connectionActionsGroup.Text = "Connection actions";
            // 
            // showWindowOnConnect
            // 
            this.showWindowOnConnect.AutoSize = true;
            this.showWindowOnConnect.Location = new System.Drawing.Point(11, 22);
            this.showWindowOnConnect.Name = "showWindowOnConnect";
            this.showWindowOnConnect.Size = new System.Drawing.Size(175, 17);
            this.showWindowOnConnect.TabIndex = 1;
            this.showWindowOnConnect.Text = "Show window when connected";
            this.showWindowOnConnect.UseVisualStyleBackColor = true;
            // 
            // startSyncOnConnect
            // 
            this.startSyncOnConnect.AutoSize = true;
            this.startSyncOnConnect.Enabled = false;
            this.startSyncOnConnect.Location = new System.Drawing.Point(11, 68);
            this.startSyncOnConnect.Name = "startSyncOnConnect";
            this.startSyncOnConnect.Size = new System.Drawing.Size(156, 17);
            this.startSyncOnConnect.TabIndex = 3;
            this.startSyncOnConnect.Text = "Start sync when connected";
            this.startSyncOnConnect.UseVisualStyleBackColor = true;
            // 
            // openExplorerOnConnect
            // 
            this.openExplorerOnConnect.AutoSize = true;
            this.openExplorerOnConnect.Location = new System.Drawing.Point(11, 45);
            this.openExplorerOnConnect.Name = "openExplorerOnConnect";
            this.openExplorerOnConnect.Size = new System.Drawing.Size(191, 17);
            this.openExplorerOnConnect.TabIndex = 2;
            this.openExplorerOnConnect.Text = "Open file explorer when connected";
            this.openExplorerOnConnect.UseVisualStyleBackColor = true;
            // 
            // securityTab
            // 
            this.securityTab.Controls.Add(this.fileTransferGroup);
            this.securityTab.Controls.Add(this.allowAll);
            this.securityTab.Location = new System.Drawing.Point(4, 22);
            this.securityTab.Name = "securityTab";
            this.securityTab.Padding = new System.Windows.Forms.Padding(3);
            this.securityTab.Size = new System.Drawing.Size(409, 231);
            this.securityTab.TabIndex = 1;
            this.securityTab.Text = "Security";
            this.securityTab.UseVisualStyleBackColor = true;
            // 
            // fileTransferGroup
            // 
            this.fileTransferGroup.Controls.Add(this.protectSystem);
            this.fileTransferGroup.Controls.Add(this.showHidden);
            this.fileTransferGroup.Controls.Add(this.browseCopy);
            this.fileTransferGroup.Controls.Add(this.write);
            this.fileTransferGroup.Location = new System.Drawing.Point(11, 37);
            this.fileTransferGroup.Name = "fileTransferGroup";
            this.fileTransferGroup.Size = new System.Drawing.Size(384, 120);
            this.fileTransferGroup.TabIndex = 7;
            this.fileTransferGroup.TabStop = false;
            this.fileTransferGroup.Text = "File Transfer";
            // 
            // protectSystem
            // 
            this.protectSystem.AutoSize = true;
            this.protectSystem.Location = new System.Drawing.Point(11, 91);
            this.protectSystem.Name = "protectSystem";
            this.protectSystem.Size = new System.Drawing.Size(192, 17);
            this.protectSystem.TabIndex = 7;
            this.protectSystem.Text = "Protect system files (recommended)";
            this.protectSystem.UseVisualStyleBackColor = true;
            // 
            // showHidden
            // 
            this.showHidden.AutoSize = true;
            this.showHidden.Location = new System.Drawing.Point(11, 68);
            this.showHidden.Name = "showHidden";
            this.showHidden.Size = new System.Drawing.Size(157, 17);
            this.showHidden.TabIndex = 6;
            this.showHidden.Text = "See hidden and system files";
            this.showHidden.UseVisualStyleBackColor = true;
            // 
            // browseCopy
            // 
            this.browseCopy.AutoSize = true;
            this.browseCopy.Location = new System.Drawing.Point(11, 22);
            this.browseCopy.Name = "browseCopy";
            this.browseCopy.Size = new System.Drawing.Size(108, 17);
            this.browseCopy.TabIndex = 3;
            this.browseCopy.Text = "Browse and copy";
            this.browseCopy.UseVisualStyleBackColor = true;
            // 
            // write
            // 
            this.write.AutoSize = true;
            this.write.Location = new System.Drawing.Point(11, 45);
            this.write.Name = "write";
            this.write.Size = new System.Drawing.Size(104, 17);
            this.write.TabIndex = 4;
            this.write.Text = "Write and delete";
            this.write.UseVisualStyleBackColor = true;
            // 
            // allowAll
            // 
            this.allowAll.AutoSize = true;
            this.allowAll.Location = new System.Drawing.Point(11, 14);
            this.allowAll.Name = "allowAll";
            this.allowAll.Size = new System.Drawing.Size(125, 17);
            this.allowAll.TabIndex = 6;
            this.allowAll.Text = "Allow all connections";
            this.allowAll.UseVisualStyleBackColor = true;
            // 
            // trustedDevicesTab
            // 
            this.trustedDevicesTab.Controls.Add(this.trustedHint);
            this.trustedDevicesTab.Controls.Add(this.trustedList);
            this.trustedDevicesTab.Controls.Add(this.editButton);
            this.trustedDevicesTab.Controls.Add(this.removeButton);
            this.trustedDevicesTab.Location = new System.Drawing.Point(4, 22);
            this.trustedDevicesTab.Name = "trustedDevicesTab";
            this.trustedDevicesTab.Size = new System.Drawing.Size(409, 231);
            this.trustedDevicesTab.TabIndex = 4;
            this.trustedDevicesTab.Text = "Trusted Devices";
            this.trustedDevicesTab.UseVisualStyleBackColor = true;
            // 
            // trustedHint
            // 
            this.trustedHint.AutoSize = true;
            this.trustedHint.Location = new System.Drawing.Point(11, 15);
            this.trustedHint.Name = "trustedHint";
            this.trustedHint.Size = new System.Drawing.Size(335, 13);
            this.trustedHint.TabIndex = 6;
            this.trustedHint.Text = "View or edit the permissions of computers you have trusted in the past";
            // 
            // trustedList
            // 
            this.trustedList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.trustedList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.nameColumn,
            this.accessColumn,
            this.lastSeenColumn});
            this.trustedList.FullRowSelect = true;
            this.trustedList.HideSelection = false;
            this.trustedList.Location = new System.Drawing.Point(14, 40);
            this.trustedList.MultiSelect = false;
            this.trustedList.Name = "trustedList";
            this.trustedList.Size = new System.Drawing.Size(382, 149);
            this.trustedList.TabIndex = 3;
            this.trustedList.UseCompatibleStateImageBehavior = false;
            this.trustedList.View = System.Windows.Forms.View.Details;
            // 
            // nameColumn
            // 
            this.nameColumn.Text = "Name";
            this.nameColumn.Width = 110;
            // 
            // accessColumn
            // 
            this.accessColumn.Text = "Access";
            this.accessColumn.Width = 150;
            // 
            // lastSeenColumn
            // 
            this.lastSeenColumn.Text = "Last seen";
            this.lastSeenColumn.Width = 96;
            // 
            // editButton
            // 
            this.editButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.editButton.Location = new System.Drawing.Point(240, 195);
            this.editButton.Name = "editButton";
            this.editButton.Size = new System.Drawing.Size(75, 23);
            this.editButton.TabIndex = 4;
            this.editButton.Text = "Edit";
            this.editButton.UseVisualStyleBackColor = true;
            // 
            // removeButton
            // 
            this.removeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.removeButton.Location = new System.Drawing.Point(321, 195);
            this.removeButton.Name = "removeButton";
            this.removeButton.Size = new System.Drawing.Size(75, 23);
            this.removeButton.TabIndex = 5;
            this.removeButton.Text = "Remove";
            this.removeButton.UseVisualStyleBackColor = true;
            // 
            // fileSharingTab
            // 
            this.fileSharingTab.Controls.Add(this.shareProfile);
            this.fileSharingTab.Controls.Add(this.shareFixed);
            this.fileSharingTab.Controls.Add(this.shareRemovable);
            this.fileSharingTab.Controls.Add(this.fixedReadOnly);
            this.fileSharingTab.Controls.Add(this.removableReadOnly);
            this.fileSharingTab.Controls.Add(this.sharedFoldersLabel);
            this.fileSharingTab.Controls.Add(this.sharedList);
            this.fileSharingTab.Controls.Add(this.addFolderButton);
            this.fileSharingTab.Controls.Add(this.addDriveButton);
            this.fileSharingTab.Controls.Add(this.toggleReadOnlyButton);
            this.fileSharingTab.Controls.Add(this.removeShareButton);
            this.fileSharingTab.Location = new System.Drawing.Point(4, 22);
            this.fileSharingTab.Name = "fileSharingTab";
            this.fileSharingTab.Padding = new System.Windows.Forms.Padding(3);
            this.fileSharingTab.Size = new System.Drawing.Size(409, 231);
            this.fileSharingTab.TabIndex = 3;
            this.fileSharingTab.Text = "File Sharing";
            this.fileSharingTab.UseVisualStyleBackColor = true;
            // 
            // shareProfile
            // 
            this.shareProfile.AutoSize = true;
            this.shareProfile.Location = new System.Drawing.Point(11, 14);
            this.shareProfile.Name = "shareProfile";
            this.shareProfile.Size = new System.Drawing.Size(290, 17);
            this.shareProfile.TabIndex = 0;
            this.shareProfile.Text = "Share my user profile folder (documents, music, pictures)";
            this.shareProfile.UseVisualStyleBackColor = true;
            // 
            // shareFixed
            // 
            this.shareFixed.AutoSize = true;
            this.shareFixed.Location = new System.Drawing.Point(11, 37);
            this.shareFixed.Name = "shareFixed";
            this.shareFixed.Size = new System.Drawing.Size(123, 17);
            this.shareFixed.TabIndex = 1;
            this.shareFixed.Text = "Share all fixed drives";
            this.shareFixed.UseVisualStyleBackColor = true;
            // 
            // shareRemovable
            // 
            this.shareRemovable.AutoSize = true;
            this.shareRemovable.Location = new System.Drawing.Point(11, 60);
            this.shareRemovable.Name = "shareRemovable";
            this.shareRemovable.Size = new System.Drawing.Size(150, 17);
            this.shareRemovable.TabIndex = 2;
            this.shareRemovable.Text = "Share all removable drives";
            this.shareRemovable.UseVisualStyleBackColor = true;
            // 
            // fixedReadOnly
            // 
            this.fixedReadOnly.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.fixedReadOnly.AutoSize = true;
            this.fixedReadOnly.Location = new System.Drawing.Point(321, 37);
            this.fixedReadOnly.Name = "fixedReadOnly";
            this.fixedReadOnly.Size = new System.Drawing.Size(74, 17);
            this.fixedReadOnly.TabIndex = 4;
            this.fixedReadOnly.Text = "Read-only";
            this.fixedReadOnly.UseVisualStyleBackColor = true;
            // 
            // removableReadOnly
            // 
            this.removableReadOnly.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.removableReadOnly.AutoSize = true;
            this.removableReadOnly.Location = new System.Drawing.Point(321, 60);
            this.removableReadOnly.Name = "removableReadOnly";
            this.removableReadOnly.Size = new System.Drawing.Size(74, 17);
            this.removableReadOnly.TabIndex = 5;
            this.removableReadOnly.Text = "Read-only";
            this.removableReadOnly.UseVisualStyleBackColor = true;
            // 
            // sharedFoldersLabel
            // 
            this.sharedFoldersLabel.AutoSize = true;
            this.sharedFoldersLabel.Location = new System.Drawing.Point(11, 85);
            this.sharedFoldersLabel.Name = "sharedFoldersLabel";
            this.sharedFoldersLabel.Size = new System.Drawing.Size(154, 13);
            this.sharedFoldersLabel.TabIndex = 6;
            this.sharedFoldersLabel.Text = "Other shared folders and drives";
            // 
            // sharedList
            // 
            this.sharedList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sharedList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.sharedNameColumn,
            this.sharedPathColumn,
            this.sharedReadOnlyColumn});
            this.sharedList.FullRowSelect = true;
            this.sharedList.HideSelection = false;
            this.sharedList.Location = new System.Drawing.Point(11, 103);
            this.sharedList.MultiSelect = false;
            this.sharedList.Name = "sharedList";
            this.sharedList.Size = new System.Drawing.Size(298, 111);
            this.sharedList.TabIndex = 7;
            this.sharedList.UseCompatibleStateImageBehavior = false;
            this.sharedList.View = System.Windows.Forms.View.Details;
            this.sharedList.SelectedIndexChanged += new System.EventHandler(this.sharedList_SelectedIndexChanged);
            this.sharedList.DoubleClick += new System.EventHandler(this.toggleReadOnlyButton_Click);
            // 
            // sharedNameColumn
            // 
            this.sharedNameColumn.Text = "Name";
            this.sharedNameColumn.Width = 80;
            // 
            // sharedPathColumn
            // 
            this.sharedPathColumn.Text = "Folder";
            this.sharedPathColumn.Width = 130;
            // 
            // sharedReadOnlyColumn
            // 
            this.sharedReadOnlyColumn.Text = "Read-only";
            this.sharedReadOnlyColumn.Width = 70;
            // 
            // addFolderButton
            // 
            this.addFolderButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.addFolderButton.Location = new System.Drawing.Point(314, 104);
            this.addFolderButton.Name = "addFolderButton";
            this.addFolderButton.Size = new System.Drawing.Size(86, 23);
            this.addFolderButton.TabIndex = 8;
            this.addFolderButton.Text = "Add folder...";
            this.addFolderButton.UseVisualStyleBackColor = true;
            this.addFolderButton.Click += new System.EventHandler(this.addFolderButton_Click);
            // 
            // addDriveButton
            // 
            this.addDriveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.addDriveButton.Location = new System.Drawing.Point(314, 132);
            this.addDriveButton.Name = "addDriveButton";
            this.addDriveButton.Size = new System.Drawing.Size(86, 23);
            this.addDriveButton.TabIndex = 9;
            this.addDriveButton.Text = "Add drive...";
            this.addDriveButton.UseVisualStyleBackColor = true;
            this.addDriveButton.Click += new System.EventHandler(this.addDriveButton_Click);
            // 
            // toggleReadOnlyButton
            // 
            this.toggleReadOnlyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.toggleReadOnlyButton.Location = new System.Drawing.Point(314, 160);
            this.toggleReadOnlyButton.Name = "toggleReadOnlyButton";
            this.toggleReadOnlyButton.Size = new System.Drawing.Size(86, 23);
            this.toggleReadOnlyButton.TabIndex = 10;
            this.toggleReadOnlyButton.Text = "Read-only on/off";
            this.toggleReadOnlyButton.UseVisualStyleBackColor = true;
            this.toggleReadOnlyButton.Click += new System.EventHandler(this.toggleReadOnlyButton_Click);
            // 
            // removeShareButton
            // 
            this.removeShareButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.removeShareButton.Location = new System.Drawing.Point(314, 188);
            this.removeShareButton.Name = "removeShareButton";
            this.removeShareButton.Size = new System.Drawing.Size(86, 23);
            this.removeShareButton.TabIndex = 11;
            this.removeShareButton.Text = "Remove";
            this.removeShareButton.UseVisualStyleBackColor = true;
            this.removeShareButton.Click += new System.EventHandler(this.removeShareButton_Click);
            // 
            // informationTab
            // 
            this.informationTab.Controls.Add(this.computerNameLabel);
            this.informationTab.Controls.Add(this.computerNameBox);
            this.informationTab.Controls.Add(this.computerIdLabel);
            this.informationTab.Controls.Add(this.computerIdBox);
            this.informationTab.Location = new System.Drawing.Point(4, 22);
            this.informationTab.Name = "informationTab";
            this.informationTab.Padding = new System.Windows.Forms.Padding(3);
            this.informationTab.Size = new System.Drawing.Size(409, 231);
            this.informationTab.TabIndex = 2;
            this.informationTab.Text = "Information";
            this.informationTab.UseVisualStyleBackColor = true;
            // 
            // computerNameLabel
            // 
            this.computerNameLabel.AutoSize = true;
            this.computerNameLabel.Location = new System.Drawing.Point(7, 16);
            this.computerNameLabel.Name = "computerNameLabel";
            this.computerNameLabel.Size = new System.Drawing.Size(81, 13);
            this.computerNameLabel.TabIndex = 5;
            this.computerNameLabel.Text = "Computer name";
            // 
            // computerNameBox
            // 
            this.computerNameBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.computerNameBox.BackColor = System.Drawing.SystemColors.Window;
            this.computerNameBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.computerNameBox.Location = new System.Drawing.Point(137, 16);
            this.computerNameBox.Name = "computerNameBox";
            this.computerNameBox.ReadOnly = true;
            this.computerNameBox.Size = new System.Drawing.Size(255, 13);
            this.computerNameBox.TabIndex = 6;
            this.computerNameBox.Text = "Computer name";
            // 
            // computerIdLabel
            // 
            this.computerIdLabel.AutoSize = true;
            this.computerIdLabel.Location = new System.Drawing.Point(7, 46);
            this.computerIdLabel.Name = "computerIdLabel";
            this.computerIdLabel.Size = new System.Drawing.Size(66, 13);
            this.computerIdLabel.TabIndex = 7;
            this.computerIdLabel.Text = "Computer ID";
            // 
            // computerIdBox
            // 
            this.computerIdBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.computerIdBox.BackColor = System.Drawing.SystemColors.Window;
            this.computerIdBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.computerIdBox.Location = new System.Drawing.Point(137, 46);
            this.computerIdBox.Name = "computerIdBox";
            this.computerIdBox.ReadOnly = true;
            this.computerIdBox.Size = new System.Drawing.Size(255, 13);
            this.computerIdBox.TabIndex = 8;
            this.computerIdBox.Text = "Computer ID";
            // 
            // OptionsForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(431, 300);
            this.Controls.Add(this.tabs);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.cancelButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OptionsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "OptionsForm";
            this.tabs.ResumeLayout(false);
            this.generalTab.ResumeLayout(false);
            this.generalTab.PerformLayout();
            this.connectionActionsGroup.ResumeLayout(false);
            this.connectionActionsGroup.PerformLayout();
            this.securityTab.ResumeLayout(false);
            this.securityTab.PerformLayout();
            this.fileTransferGroup.ResumeLayout(false);
            this.fileTransferGroup.PerformLayout();
            this.trustedDevicesTab.ResumeLayout(false);
            this.trustedDevicesTab.PerformLayout();
            this.fileSharingTab.ResumeLayout(false);
            this.fileSharingTab.PerformLayout();
            this.informationTab.ResumeLayout(false);
            this.informationTab.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox startAtLogon;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage generalTab;
        private System.Windows.Forms.TabPage securityTab;
        private System.Windows.Forms.CheckBox allowAll;
        private System.Windows.Forms.TabPage informationTab;
        private System.Windows.Forms.Label computerNameLabel;
        private System.Windows.Forms.TextBox computerNameBox;
        private System.Windows.Forms.Label computerIdLabel;
        private System.Windows.Forms.TextBox computerIdBox;
        private System.Windows.Forms.TabPage fileSharingTab;
        private System.Windows.Forms.CheckBox shareProfile;
        private System.Windows.Forms.CheckBox shareFixed;
        private System.Windows.Forms.CheckBox shareRemovable;
        private System.Windows.Forms.CheckBox fixedReadOnly;
        private System.Windows.Forms.CheckBox removableReadOnly;
        private System.Windows.Forms.Label sharedFoldersLabel;
        private System.Windows.Forms.ListView sharedList;
        private System.Windows.Forms.ColumnHeader sharedNameColumn;
        private System.Windows.Forms.ColumnHeader sharedPathColumn;
        private System.Windows.Forms.ColumnHeader sharedReadOnlyColumn;
        private System.Windows.Forms.Button addFolderButton;
        private System.Windows.Forms.Button addDriveButton;
        private System.Windows.Forms.Button toggleReadOnlyButton;
        private System.Windows.Forms.Button removeShareButton;
        private System.Windows.Forms.TabPage trustedDevicesTab;
        private System.Windows.Forms.ListView trustedList;
        private System.Windows.Forms.ColumnHeader nameColumn;
        private System.Windows.Forms.ColumnHeader accessColumn;
        private System.Windows.Forms.ColumnHeader lastSeenColumn;
        private System.Windows.Forms.Button editButton;
        private System.Windows.Forms.Button removeButton;
        private System.Windows.Forms.Label trustedHint;
        private System.Windows.Forms.GroupBox fileTransferGroup;
        private System.Windows.Forms.CheckBox browseCopy;
        private System.Windows.Forms.CheckBox write;
        private System.Windows.Forms.CheckBox showWindowOnConnect;
        private System.Windows.Forms.CheckBox openExplorerOnConnect;
        private System.Windows.Forms.CheckBox startSyncOnConnect;
        private System.Windows.Forms.GroupBox connectionActionsGroup;
        private System.Windows.Forms.CheckBox protectSystem;
        private System.Windows.Forms.CheckBox showHidden;
    }
}
