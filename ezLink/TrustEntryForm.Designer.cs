namespace ezLink
{
    partial class TrustEntryForm
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
            this.browseCopy = new System.Windows.Forms.CheckBox();
            this.write = new System.Windows.Forms.CheckBox();
            this.block = new System.Windows.Forms.CheckBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // browseCopy
            // 
            this.browseCopy.AutoSize = true;
            this.browseCopy.Location = new System.Drawing.Point(14, 16);
            this.browseCopy.Name = "browseCopy";
            this.browseCopy.Size = new System.Drawing.Size(116, 19);
            this.browseCopy.TabIndex = 0;
            this.browseCopy.Text = "Browse and copy";
            this.browseCopy.UseVisualStyleBackColor = true;
            // 
            // write
            // 
            this.write.AutoSize = true;
            this.write.Location = new System.Drawing.Point(14, 42);
            this.write.Name = "write";
            this.write.Size = new System.Drawing.Size(54, 19);
            this.write.TabIndex = 1;
            this.write.Text = "Write";
            this.write.UseVisualStyleBackColor = true;
            // 
            // block
            // 
            this.block.AutoSize = true;
            this.block.Location = new System.Drawing.Point(14, 78);
            this.block.Name = "block";
            this.block.Size = new System.Drawing.Size(55, 19);
            this.block.TabIndex = 2;
            this.block.Text = "Block";
            this.block.UseVisualStyleBackColor = true;
            this.block.CheckedChanged += new System.EventHandler(this.block_CheckedChanged);
            // 
            // okButton
            // 
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(230, 128);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 26);
            this.okButton.TabIndex = 3;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(311, 128);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 26);
            this.cancelButton.TabIndex = 4;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // TrustEntryForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(400, 170);
            this.Controls.Add(this.browseCopy);
            this.Controls.Add(this.write);
            this.Controls.Add(this.block);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.cancelButton);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TrustEntryForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "TrustEntryForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox browseCopy;
        private System.Windows.Forms.CheckBox write;
        private System.Windows.Forms.CheckBox block;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
    }
}
