namespace ezLink
{
    partial class ApprovalForm
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
            this.browseCopy = new System.Windows.Forms.CheckBox();
            this.write = new System.Windows.Forms.CheckBox();
            this.note = new System.Windows.Forms.Label();
            this.allowOnce = new System.Windows.Forms.Button();
            this.alwaysAllow = new System.Windows.Forms.Button();
            this.deny = new System.Windows.Forms.Button();
            this.watchdog = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // headline
            // 
            this.headline.AutoEllipsis = true;
            this.headline.Location = new System.Drawing.Point(14, 14);
            this.headline.Name = "headline";
            this.headline.Size = new System.Drawing.Size(412, 38);
            this.headline.TabIndex = 0;
            this.headline.Text = "headline";
            // 
            // browseCopy
            // 
            this.browseCopy.AutoSize = true;
            this.browseCopy.Checked = true;
            this.browseCopy.CheckState = System.Windows.Forms.CheckState.Checked;
            this.browseCopy.Location = new System.Drawing.Point(24, 58);
            this.browseCopy.Name = "browseCopy";
            this.browseCopy.Size = new System.Drawing.Size(116, 19);
            this.browseCopy.TabIndex = 1;
            this.browseCopy.Text = "Browse and copy";
            this.browseCopy.UseVisualStyleBackColor = true;
            this.browseCopy.CheckedChanged += new System.EventHandler(this.OnPermissionsEdited);
            // 
            // write
            // 
            this.write.AutoSize = true;
            this.write.Location = new System.Drawing.Point(24, 82);
            this.write.Name = "write";
            this.write.Size = new System.Drawing.Size(54, 19);
            this.write.TabIndex = 2;
            this.write.Text = "Write";
            this.write.UseVisualStyleBackColor = true;
            this.write.CheckedChanged += new System.EventHandler(this.OnPermissionsEdited);
            // 
            // note
            // 
            this.note.ForeColor = System.Drawing.SystemColors.GrayText;
            this.note.Location = new System.Drawing.Point(14, 110);
            this.note.Name = "note";
            this.note.Size = new System.Drawing.Size(412, 34);
            this.note.TabIndex = 3;
            this.note.Text = "note";
            // 
            // allowOnce
            // 
            this.allowOnce.Location = new System.Drawing.Point(14, 154);
            this.allowOnce.Name = "allowOnce";
            this.allowOnce.Size = new System.Drawing.Size(110, 28);
            this.allowOnce.TabIndex = 4;
            this.allowOnce.Text = "Allow once";
            this.allowOnce.UseVisualStyleBackColor = true;
            this.allowOnce.Click += new System.EventHandler(this.allowOnce_Click);
            // 
            // alwaysAllow
            // 
            this.alwaysAllow.Location = new System.Drawing.Point(132, 154);
            this.alwaysAllow.Name = "alwaysAllow";
            this.alwaysAllow.Size = new System.Drawing.Size(110, 28);
            this.alwaysAllow.TabIndex = 5;
            this.alwaysAllow.Text = "Always allow";
            this.alwaysAllow.UseVisualStyleBackColor = true;
            this.alwaysAllow.Click += new System.EventHandler(this.alwaysAllow_Click);
            // 
            // deny
            // 
            this.deny.Location = new System.Drawing.Point(316, 154);
            this.deny.Name = "deny";
            this.deny.Size = new System.Drawing.Size(110, 28);
            this.deny.TabIndex = 6;
            this.deny.Text = "Deny";
            this.deny.UseVisualStyleBackColor = true;
            this.deny.Click += new System.EventHandler(this.deny_Click);
            // 
            // watchdog
            // 
            this.watchdog.Interval = 250;
            this.watchdog.Tick += new System.EventHandler(this.OnWatchdog);
            // 
            // ApprovalForm
            // 
            this.AcceptButton = this.deny;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(440, 196);
            this.Controls.Add(this.headline);
            this.Controls.Add(this.browseCopy);
            this.Controls.Add(this.write);
            this.Controls.Add(this.note);
            this.Controls.Add(this.allowOnce);
            this.Controls.Add(this.alwaysAllow);
            this.Controls.Add(this.deny);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ApprovalForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ApprovalForm";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label headline;
        private System.Windows.Forms.CheckBox browseCopy;
        private System.Windows.Forms.CheckBox write;
        private System.Windows.Forms.Label note;
        private System.Windows.Forms.Button allowOnce;
        private System.Windows.Forms.Button alwaysAllow;
        private System.Windows.Forms.Button deny;
        private System.Windows.Forms.Timer watchdog;
    }
}
