namespace ezExplorer
{
    partial class TransferDialog
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
            this.nameLabel = new System.Windows.Forms.Label();
            this.pathLabel = new System.Windows.Forms.Label();
            this.fileBar = new System.Windows.Forms.ProgressBar();
            this.totalBar = new System.Windows.Forms.ProgressBar();
            this.detail = new System.Windows.Forms.Label();
            this.cancelButton = new System.Windows.Forms.Button();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.mainPanel = new System.Windows.Forms.Panel();
            this.footerPanel = new System.Windows.Forms.Panel();
            this.toolbarDivider1 = new ezExplorer.ToolbarDivider();
            this.headline = new ezExplorer.DialogHeadline();
            this.mainPanel.SuspendLayout();
            this.footerPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // nameLabel
            // 
            this.nameLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.nameLabel.AutoEllipsis = true;
            this.nameLabel.Location = new System.Drawing.Point(23, 12);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(369, 18);
            this.nameLabel.TabIndex = 1;
            this.nameLabel.Text = "nameLabel";
            // 
            // pathLabel
            // 
            this.pathLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pathLabel.AutoEllipsis = true;
            this.pathLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.pathLabel.Location = new System.Drawing.Point(23, 32);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(369, 18);
            this.pathLabel.TabIndex = 2;
            this.pathLabel.Text = "pathLabel";
            // 
            // fileBar
            // 
            this.fileBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.fileBar.Location = new System.Drawing.Point(23, 56);
            this.fileBar.Maximum = 1000;
            this.fileBar.Name = "fileBar";
            this.fileBar.Size = new System.Drawing.Size(369, 18);
            this.fileBar.TabIndex = 3;
            this.fileBar.Visible = false;
            // 
            // totalBar
            // 
            this.totalBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.totalBar.Location = new System.Drawing.Point(23, 80);
            this.totalBar.Maximum = 1000;
            this.totalBar.Name = "totalBar";
            this.totalBar.Size = new System.Drawing.Size(369, 18);
            this.totalBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.totalBar.TabIndex = 4;
            // 
            // detail
            // 
            this.detail.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.detail.AutoEllipsis = true;
            this.detail.Location = new System.Drawing.Point(23, 106);
            this.detail.Name = "detail";
            this.detail.Size = new System.Drawing.Size(369, 34);
            this.detail.TabIndex = 5;
            this.detail.Text = "detailLabel";
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(317, 8);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 6;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // timer
            // 
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // mainPanel
            // 
            this.mainPanel.BackColor = System.Drawing.SystemColors.Window;
            this.mainPanel.Controls.Add(this.nameLabel);
            this.mainPanel.Controls.Add(this.detail);
            this.mainPanel.Controls.Add(this.totalBar);
            this.mainPanel.Controls.Add(this.fileBar);
            this.mainPanel.Controls.Add(this.pathLabel);
            this.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainPanel.Location = new System.Drawing.Point(0, 40);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Size = new System.Drawing.Size(414, 147);
            this.mainPanel.TabIndex = 7;
            // 
            // footerPanel
            // 
            this.footerPanel.Controls.Add(this.toolbarDivider1);
            this.footerPanel.Controls.Add(this.cancelButton);
            this.footerPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.footerPanel.Location = new System.Drawing.Point(0, 187);
            this.footerPanel.Name = "footerPanel";
            this.footerPanel.Size = new System.Drawing.Size(414, 40);
            this.footerPanel.TabIndex = 8;
            // 
            // toolbarDivider1
            // 
            this.toolbarDivider1.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolbarDivider1.Location = new System.Drawing.Point(0, 0);
            this.toolbarDivider1.Name = "toolbarDivider1";
            this.toolbarDivider1.Size = new System.Drawing.Size(414, 1);
            this.toolbarDivider1.TabIndex = 7;
            this.toolbarDivider1.TabStop = false;
            this.toolbarDivider1.Text = "toolbarDivider1";
            // 
            // headline
            // 
            this.headline.Dock = System.Windows.Forms.DockStyle.Top;
            this.headline.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.headline.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(57)))), ((int)(((byte)(91)))));
            this.headline.Location = new System.Drawing.Point(0, 0);
            this.headline.Name = "headline";
            this.headline.Padding = new System.Windows.Forms.Padding(7, 0, 0, 0);
            this.headline.Size = new System.Drawing.Size(414, 40);
            this.headline.TabIndex = 0;
            this.headline.TabStop = false;
            this.headline.Text = "headline";
            // 
            // TransferDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(414, 227);
            this.Controls.Add(this.mainPanel);
            this.Controls.Add(this.footerPanel);
            this.Controls.Add(this.headline);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TransferDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "TransferDialog";
            this.mainPanel.ResumeLayout(false);
            this.footerPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ezExplorer.DialogHeadline headline;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.ProgressBar fileBar;
        private System.Windows.Forms.ProgressBar totalBar;
        private System.Windows.Forms.Label detail;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.Panel mainPanel;
        private System.Windows.Forms.Panel footerPanel;
        private ToolbarDivider toolbarDivider1;
    }
}
