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
            this.headline = new System.Windows.Forms.Label();
            this.nameLabel = new System.Windows.Forms.Label();
            this.pathLabel = new System.Windows.Forms.Label();
            this.fileBar = new System.Windows.Forms.ProgressBar();
            this.totalBar = new System.Windows.Forms.ProgressBar();
            this.detail = new System.Windows.Forms.Label();
            this.cancelButton = new System.Windows.Forms.Button();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // headline
            // 
            this.headline.Location = new System.Drawing.Point(12, 12);
            this.headline.Name = "headline";
            this.headline.Size = new System.Drawing.Size(456, 20);
            this.headline.TabIndex = 0;
            this.headline.Text = "headline";
            // 
            // nameLabel
            // 
            this.nameLabel.AutoEllipsis = true;
            this.nameLabel.Location = new System.Drawing.Point(12, 38);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(456, 18);
            this.nameLabel.TabIndex = 1;
            this.nameLabel.Text = "nameLabel";
            // 
            // pathLabel
            // 
            this.pathLabel.AutoEllipsis = true;
            this.pathLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.pathLabel.Location = new System.Drawing.Point(12, 58);
            this.pathLabel.Name = "pathLabel";
            this.pathLabel.Size = new System.Drawing.Size(456, 18);
            this.pathLabel.TabIndex = 2;
            // 
            // fileBar
            // 
            this.fileBar.Location = new System.Drawing.Point(12, 84);
            this.fileBar.Maximum = 1000;
            this.fileBar.Name = "fileBar";
            this.fileBar.Size = new System.Drawing.Size(456, 18);
            this.fileBar.TabIndex = 3;
            this.fileBar.Visible = false;
            // 
            // totalBar
            // 
            this.totalBar.Location = new System.Drawing.Point(12, 108);
            this.totalBar.Maximum = 1000;
            this.totalBar.Name = "totalBar";
            this.totalBar.Size = new System.Drawing.Size(456, 18);
            this.totalBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.totalBar.TabIndex = 4;
            // 
            // detail
            // 
            this.detail.AutoEllipsis = true;
            this.detail.Location = new System.Drawing.Point(12, 134);
            this.detail.Name = "detail";
            this.detail.Size = new System.Drawing.Size(456, 36);
            this.detail.TabIndex = 5;
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(368, 162);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(100, 28);
            this.cancelButton.TabIndex = 6;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // timer
            // 
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // TransferDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(480, 200);
            this.Controls.Add(this.headline);
            this.Controls.Add(this.nameLabel);
            this.Controls.Add(this.pathLabel);
            this.Controls.Add(this.fileBar);
            this.Controls.Add(this.totalBar);
            this.Controls.Add(this.detail);
            this.Controls.Add(this.cancelButton);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TransferDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "TransferDialog";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label headline;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.ProgressBar fileBar;
        private System.Windows.Forms.ProgressBar totalBar;
        private System.Windows.Forms.Label detail;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Timer timer;
    }
}
