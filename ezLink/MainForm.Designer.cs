namespace ezLink
{
    partial class MainForm
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
            this.toolStrip = new ezLink.NativeToolStrip.NativeToolStrip();
            this.exploreButton = new System.Windows.Forms.ToolStripButton();
            this.syncButton = new System.Windows.Forms.ToolStripButton();
            this.optionsButton = new System.Windows.Forms.ToolStripButton();
            this.mainPanel = new System.Windows.Forms.Panel();
            this.fixButton = new System.Windows.Forms.Button();
            this.statusIcon = new System.Windows.Forms.PictureBox();
            this.statusLabel = new System.Windows.Forms.Label();
            this.peerLabel = new System.Windows.Forms.Label();
            this.weAllowLabel = new System.Windows.Forms.Label();
            this.peerAllowsLabel = new System.Windows.Forms.Label();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.trayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.openItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exploreItem = new System.Windows.Forms.ToolStripMenuItem();
            this.traySeparator = new System.Windows.Forms.ToolStripSeparator();
            this.exitItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip.SuspendLayout();
            this.mainPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.statusIcon)).BeginInit();
            this.trayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip
            // 
            this.toolStrip.AutoSize = false;
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exploreButton,
            this.syncButton,
            this.optionsButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Padding = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.toolStrip.Size = new System.Drawing.Size(420, 39);
            this.toolStrip.TabIndex = 1;
            this.toolStrip.Theme = ezLink.NativeToolStrip.NativeToolStripTheme.Toolbar;
            // 
            // exploreButton
            // 
            this.exploreButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.exploreButton.Name = "exploreButton";
            this.exploreButton.Size = new System.Drawing.Size(50, 36);
            this.exploreButton.Text = "Explore";
            this.exploreButton.Click += new System.EventHandler(this.OnExploreClick);
            // 
            // syncButton
            // 
            this.syncButton.Enabled = false;
            this.syncButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.syncButton.Name = "syncButton";
            this.syncButton.Size = new System.Drawing.Size(36, 36);
            this.syncButton.Text = "Sync";
            // 
            // optionsButton
            // 
            this.optionsButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.optionsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.optionsButton.Name = "optionsButton";
            this.optionsButton.Size = new System.Drawing.Size(53, 36);
            this.optionsButton.Text = "Options";
            this.optionsButton.Click += new System.EventHandler(this.OnOptionsClick);
            // 
            // mainPanel
            // 
            this.mainPanel.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.mainPanel.Controls.Add(this.fixButton);
            this.mainPanel.Controls.Add(this.statusIcon);
            this.mainPanel.Controls.Add(this.statusLabel);
            this.mainPanel.Controls.Add(this.peerLabel);
            this.mainPanel.Controls.Add(this.weAllowLabel);
            this.mainPanel.Controls.Add(this.peerAllowsLabel);
            this.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainPanel.Location = new System.Drawing.Point(0, 39);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Size = new System.Drawing.Size(420, 118);
            this.mainPanel.TabIndex = 0;
            // 
            // fixButton
            // 
            this.fixButton.Location = new System.Drawing.Point(82, 56);
            this.fixButton.Name = "fixButton";
            this.fixButton.Size = new System.Drawing.Size(90, 26);
            this.fixButton.TabIndex = 0;
            this.fixButton.Text = "Fix";
            this.fixButton.UseVisualStyleBackColor = true;
            this.fixButton.Visible = false;
            this.fixButton.Click += new System.EventHandler(this.OnFixClick);
            // 
            // statusIcon
            // 
            this.statusIcon.Location = new System.Drawing.Point(8, 8);
            this.statusIcon.Name = "statusIcon";
            this.statusIcon.Size = new System.Drawing.Size(64, 64);
            this.statusIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.statusIcon.TabIndex = 1;
            this.statusIcon.TabStop = false;
            // 
            // statusLabel
            // 
            this.statusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.statusLabel.Location = new System.Drawing.Point(80, 10);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(328, 40);
            this.statusLabel.TabIndex = 2;
            // 
            // peerLabel
            // 
            this.peerLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.peerLabel.AutoEllipsis = true;
            this.peerLabel.Location = new System.Drawing.Point(82, 52);
            this.peerLabel.Name = "peerLabel";
            this.peerLabel.Size = new System.Drawing.Size(326, 18);
            this.peerLabel.TabIndex = 3;
            // 
            // weAllowLabel
            // 
            this.weAllowLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.weAllowLabel.AutoEllipsis = true;
            this.weAllowLabel.Location = new System.Drawing.Point(82, 76);
            this.weAllowLabel.Name = "weAllowLabel";
            this.weAllowLabel.Size = new System.Drawing.Size(326, 18);
            this.weAllowLabel.TabIndex = 4;
            // 
            // peerAllowsLabel
            // 
            this.peerAllowsLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.peerAllowsLabel.AutoEllipsis = true;
            this.peerAllowsLabel.Location = new System.Drawing.Point(82, 96);
            this.peerAllowsLabel.Name = "peerAllowsLabel";
            this.peerAllowsLabel.Size = new System.Drawing.Size(326, 18);
            this.peerAllowsLabel.TabIndex = 5;
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenuStrip = this.trayMenu;
            this.notifyIcon.DoubleClick += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // trayMenu
            // 
            this.trayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openItem,
            this.exploreItem,
            this.traySeparator,
            this.exitItem});
            this.trayMenu.Name = "trayMenu";
            this.trayMenu.Size = new System.Drawing.Size(114, 76);
            // 
            // openItem
            // 
            this.openItem.Name = "openItem";
            this.openItem.Size = new System.Drawing.Size(113, 22);
            this.openItem.Text = "Open";
            this.openItem.Click += new System.EventHandler(this.openItem_Click);
            // 
            // exploreItem
            // 
            this.exploreItem.Name = "exploreItem";
            this.exploreItem.Size = new System.Drawing.Size(113, 22);
            this.exploreItem.Text = "Explore";
            this.exploreItem.Click += new System.EventHandler(this.OnExploreClick);
            // 
            // traySeparator
            // 
            this.traySeparator.Name = "traySeparator";
            this.traySeparator.Size = new System.Drawing.Size(110, 6);
            // 
            // exitItem
            // 
            this.exitItem.Name = "exitItem";
            this.exitItem.Size = new System.Drawing.Size(113, 22);
            this.exitItem.Text = "Exit";
            this.exitItem.Click += new System.EventHandler(this.exitItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(420, 157);
            this.Controls.Add(this.mainPanel);
            this.Controls.Add(this.toolStrip);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "MainForm";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.mainPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.statusIcon)).EndInit();
            this.trayMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ezLink.NativeToolStrip.NativeToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton exploreButton;
        private System.Windows.Forms.ToolStripButton syncButton;
        private System.Windows.Forms.ToolStripButton optionsButton;
        private System.Windows.Forms.Panel mainPanel;
        private System.Windows.Forms.PictureBox statusIcon;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Label peerLabel;
        private System.Windows.Forms.Label weAllowLabel;
        private System.Windows.Forms.Label peerAllowsLabel;
        private System.Windows.Forms.Button fixButton;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip trayMenu;
        private System.Windows.Forms.ToolStripMenuItem openItem;
        private System.Windows.Forms.ToolStripMenuItem exploreItem;
        private System.Windows.Forms.ToolStripSeparator traySeparator;
        private System.Windows.Forms.ToolStripMenuItem exitItem;
    }
}
