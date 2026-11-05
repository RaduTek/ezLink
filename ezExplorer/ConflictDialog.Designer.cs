namespace ezExplorer
{
    partial class ConflictDialog
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
            this.existing = new System.Windows.Forms.Label();
            this.incoming = new System.Windows.Forms.Label();
            this.applyAll = new System.Windows.Forms.CheckBox();
            this.replaceButton = new System.Windows.Forms.Button();
            this.keepButton = new System.Windows.Forms.Button();
            this.skipButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // message
            // 
            this.message.AutoEllipsis = true;
            this.message.Location = new System.Drawing.Point(12, 12);
            this.message.Name = "message";
            this.message.Size = new System.Drawing.Size(416, 36);
            this.message.TabIndex = 0;
            this.message.Text = "message";
            // 
            // existing
            // 
            this.existing.Location = new System.Drawing.Point(12, 52);
            this.existing.Name = "existing";
            this.existing.Size = new System.Drawing.Size(416, 20);
            this.existing.TabIndex = 1;
            this.existing.Text = "existing";
            // 
            // incoming
            // 
            this.incoming.Location = new System.Drawing.Point(12, 74);
            this.incoming.Name = "incoming";
            this.incoming.Size = new System.Drawing.Size(416, 20);
            this.incoming.TabIndex = 2;
            this.incoming.Text = "incoming";
            // 
            // applyAll
            // 
            this.applyAll.Location = new System.Drawing.Point(12, 104);
            this.applyAll.Name = "applyAll";
            this.applyAll.Size = new System.Drawing.Size(416, 22);
            this.applyAll.TabIndex = 3;
            this.applyAll.Text = "Apply to all";
            this.applyAll.UseVisualStyleBackColor = true;
            // 
            // replaceButton
            // 
            this.replaceButton.Location = new System.Drawing.Point(12, 142);
            this.replaceButton.Name = "replaceButton";
            this.replaceButton.Size = new System.Drawing.Size(100, 28);
            this.replaceButton.TabIndex = 4;
            this.replaceButton.Text = "Replace";
            this.replaceButton.UseVisualStyleBackColor = true;
            this.replaceButton.Click += new System.EventHandler(this.replaceButton_Click);
            // 
            // keepButton
            // 
            this.keepButton.Location = new System.Drawing.Point(120, 142);
            this.keepButton.Name = "keepButton";
            this.keepButton.Size = new System.Drawing.Size(100, 28);
            this.keepButton.TabIndex = 5;
            this.keepButton.Text = "Keep both";
            this.keepButton.UseVisualStyleBackColor = true;
            this.keepButton.Click += new System.EventHandler(this.keepButton_Click);
            // 
            // skipButton
            // 
            this.skipButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.skipButton.Location = new System.Drawing.Point(228, 142);
            this.skipButton.Name = "skipButton";
            this.skipButton.Size = new System.Drawing.Size(100, 28);
            this.skipButton.TabIndex = 6;
            this.skipButton.Text = "Skip";
            this.skipButton.UseVisualStyleBackColor = true;
            this.skipButton.Click += new System.EventHandler(this.skipButton_Click);
            // 
            // ConflictDialog
            // 
            this.AcceptButton = this.replaceButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.skipButton;
            this.ClientSize = new System.Drawing.Size(440, 190);
            this.Controls.Add(this.message);
            this.Controls.Add(this.existing);
            this.Controls.Add(this.incoming);
            this.Controls.Add(this.applyAll);
            this.Controls.Add(this.replaceButton);
            this.Controls.Add(this.keepButton);
            this.Controls.Add(this.skipButton);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConflictDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ConflictDialog";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label message;
        private System.Windows.Forms.Label existing;
        private System.Windows.Forms.Label incoming;
        private System.Windows.Forms.CheckBox applyAll;
        private System.Windows.Forms.Button replaceButton;
        private System.Windows.Forms.Button keepButton;
        private System.Windows.Forms.Button skipButton;
    }
}
