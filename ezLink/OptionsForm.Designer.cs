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
            this.computerNameLabel = new System.Windows.Forms.Label();
            this.computerNameBox = new System.Windows.Forms.TextBox();
            this.computerIdLabel = new System.Windows.Forms.Label();
            this.computerIdBox = new System.Windows.Forms.TextBox();
            this.trustedGroup = new System.Windows.Forms.GroupBox();
            this.trustedList = new System.Windows.Forms.ListView();
            this.nameColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.accessColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lastSeenColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.editButton = new System.Windows.Forms.Button();
            this.removeButton = new System.Windows.Forms.Button();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.trustedGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // startAtLogon
            // 
            this.startAtLogon.AutoSize = true;
            this.startAtLogon.Location = new System.Drawing.Point(14, 14);
            this.startAtLogon.Name = "startAtLogon";
            this.startAtLogon.Size = new System.Drawing.Size(97, 19);
            this.startAtLogon.TabIndex = 0;
            this.startAtLogon.Text = "Start at logon";
            this.startAtLogon.UseVisualStyleBackColor = true;
            // 
            // computerNameLabel
            // 
            this.computerNameLabel.AutoSize = true;
            this.computerNameLabel.Location = new System.Drawing.Point(14, 51);
            this.computerNameLabel.Name = "computerNameLabel";
            this.computerNameLabel.Size = new System.Drawing.Size(94, 15);
            this.computerNameLabel.TabIndex = 1;
            this.computerNameLabel.Text = "Computer name";
            // 
            // computerNameBox
            // 
            this.computerNameBox.Location = new System.Drawing.Point(160, 48);
            this.computerNameBox.Name = "computerNameBox";
            this.computerNameBox.ReadOnly = true;
            this.computerNameBox.Size = new System.Drawing.Size(246, 23);
            this.computerNameBox.TabIndex = 2;
            // 
            // computerIdLabel
            // 
            this.computerIdLabel.AutoSize = true;
            this.computerIdLabel.Location = new System.Drawing.Point(14, 81);
            this.computerIdLabel.Name = "computerIdLabel";
            this.computerIdLabel.Size = new System.Drawing.Size(75, 15);
            this.computerIdLabel.TabIndex = 3;
            this.computerIdLabel.Text = "Computer ID";
            // 
            // computerIdBox
            // 
            this.computerIdBox.Location = new System.Drawing.Point(160, 78);
            this.computerIdBox.Name = "computerIdBox";
            this.computerIdBox.ReadOnly = true;
            this.computerIdBox.Size = new System.Drawing.Size(246, 23);
            this.computerIdBox.TabIndex = 4;
            // 
            // trustedGroup
            // 
            this.trustedGroup.Controls.Add(this.trustedList);
            this.trustedGroup.Controls.Add(this.editButton);
            this.trustedGroup.Controls.Add(this.removeButton);
            this.trustedGroup.Location = new System.Drawing.Point(14, 110);
            this.trustedGroup.Name = "trustedGroup";
            this.trustedGroup.Size = new System.Drawing.Size(392, 196);
            this.trustedGroup.TabIndex = 5;
            this.trustedGroup.TabStop = false;
            this.trustedGroup.Text = "Trusted computers";
            // 
            // trustedList
            // 
            this.trustedList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.nameColumn,
            this.accessColumn,
            this.lastSeenColumn});
            this.trustedList.FullRowSelect = true;
            this.trustedList.HideSelection = false;
            this.trustedList.Location = new System.Drawing.Point(10, 22);
            this.trustedList.MultiSelect = false;
            this.trustedList.Name = "trustedList";
            this.trustedList.Size = new System.Drawing.Size(372, 128);
            this.trustedList.TabIndex = 0;
            this.trustedList.UseCompatibleStateImageBehavior = false;
            this.trustedList.View = System.Windows.Forms.View.Details;
            this.trustedList.SelectedIndexChanged += new System.EventHandler(this.trustedList_SelectedIndexChanged);
            this.trustedList.DoubleClick += new System.EventHandler(this.trustedList_DoubleClick);
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
            this.editButton.Location = new System.Drawing.Point(226, 158);
            this.editButton.Name = "editButton";
            this.editButton.Size = new System.Drawing.Size(75, 26);
            this.editButton.TabIndex = 1;
            this.editButton.Text = "Edit";
            this.editButton.UseVisualStyleBackColor = true;
            this.editButton.Click += new System.EventHandler(this.editButton_Click);
            // 
            // removeButton
            // 
            this.removeButton.Location = new System.Drawing.Point(307, 158);
            this.removeButton.Name = "removeButton";
            this.removeButton.Size = new System.Drawing.Size(75, 26);
            this.removeButton.TabIndex = 2;
            this.removeButton.Text = "Remove";
            this.removeButton.UseVisualStyleBackColor = true;
            this.removeButton.Click += new System.EventHandler(this.removeButton_Click);
            // 
            // okButton
            // 
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(250, 320);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 26);
            this.okButton.TabIndex = 6;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(331, 320);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 26);
            this.cancelButton.TabIndex = 7;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // OptionsForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(420, 360);
            this.Controls.Add(this.startAtLogon);
            this.Controls.Add(this.computerNameLabel);
            this.Controls.Add(this.computerNameBox);
            this.Controls.Add(this.computerIdLabel);
            this.Controls.Add(this.computerIdBox);
            this.Controls.Add(this.trustedGroup);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.cancelButton);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OptionsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "OptionsForm";
            this.trustedGroup.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox startAtLogon;
        private System.Windows.Forms.Label computerNameLabel;
        private System.Windows.Forms.TextBox computerNameBox;
        private System.Windows.Forms.Label computerIdLabel;
        private System.Windows.Forms.TextBox computerIdBox;
        private System.Windows.Forms.GroupBox trustedGroup;
        private System.Windows.Forms.ListView trustedList;
        private System.Windows.Forms.ColumnHeader nameColumn;
        private System.Windows.Forms.ColumnHeader accessColumn;
        private System.Windows.Forms.ColumnHeader lastSeenColumn;
        private System.Windows.Forms.Button editButton;
        private System.Windows.Forms.Button removeButton;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
    }
}
