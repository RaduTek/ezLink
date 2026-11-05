namespace ezExplorer
{
    partial class CopyErrorDialog
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
            this.message = new System.Windows.Forms.Label();
            this.retryButton = new System.Windows.Forms.Button();
            this.skipButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // message
            // 
            this.message.Location = new System.Drawing.Point(12, 12);
            this.message.Name = "message";
            this.message.Size = new System.Drawing.Size(416, 90);
            this.message.TabIndex = 0;
            this.message.Text = "message";
            // 
            // retryButton
            // 
            this.retryButton.Location = new System.Drawing.Point(12, 116);
            this.retryButton.Name = "retryButton";
            this.retryButton.Size = new System.Drawing.Size(100, 28);
            this.retryButton.TabIndex = 1;
            this.retryButton.Text = "Retry";
            this.retryButton.UseVisualStyleBackColor = true;
            this.retryButton.Click += new System.EventHandler(this.retryButton_Click);
            // 
            // skipButton
            // 
            this.skipButton.Location = new System.Drawing.Point(120, 116);
            this.skipButton.Name = "skipButton";
            this.skipButton.Size = new System.Drawing.Size(100, 28);
            this.skipButton.TabIndex = 2;
            this.skipButton.Text = "Skip";
            this.skipButton.UseVisualStyleBackColor = true;
            this.skipButton.Click += new System.EventHandler(this.skipButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(228, 116);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(100, 28);
            this.cancelButton.TabIndex = 3;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // CopyErrorDialog
            // 
            this.AcceptButton = this.retryButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(440, 160);
            this.Controls.Add(this.message);
            this.Controls.Add(this.retryButton);
            this.Controls.Add(this.skipButton);
            this.Controls.Add(this.cancelButton);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CopyErrorDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "CopyErrorDialog";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label message;
        private System.Windows.Forms.Button retryButton;
        private System.Windows.Forms.Button skipButton;
        private System.Windows.Forms.Button cancelButton;
    }
}
