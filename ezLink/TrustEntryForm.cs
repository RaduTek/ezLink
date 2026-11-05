using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;
using EzLink.Protocol;

namespace ezLink
{
    /// <summary>Edits what one trusted computer may do on this computer, or blocks it.</summary>
    internal sealed class TrustEntryForm : Form
    {
        private readonly TrustEntry entry;
        private readonly CheckBox browseCopy = new CheckBox();
        private readonly CheckBox write = new CheckBox();
        private readonly CheckBox block = new CheckBox();

        public TrustEntryForm(TrustEntry entry)
        {
            this.entry = entry;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = string.Format(Strings.Get("TrustEditTitle"), entry.ComputerName);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 170);

            browseCopy.Text = Strings.Get("ApprovalBrowseCopy");
            browseCopy.AutoSize = true;
            browseCopy.Location = new Point(14, 16);
            browseCopy.Checked = (entry.Permissions & (Permissions.Browse | Permissions.Read)) != 0;
            Controls.Add(browseCopy);

            write.Text = Strings.Get("ApprovalWrite");
            write.AutoSize = true;
            write.Location = new Point(14, 42);
            write.Checked = (entry.Permissions & Permissions.Write) != 0;
            Controls.Add(write);

            block.Text = Strings.Get("TrustEditBlock");
            block.AutoSize = true;
            block.Location = new Point(14, 78);
            block.Checked = entry.Decision == TrustDecision.Deny;
            block.CheckedChanged += delegate { UpdateEnabled(); };
            Controls.Add(block);

            Button ok = new Button();
            ok.Text = Strings.Get("ButtonOk");
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(230, 128);
            ok.Size = new Size(75, 26);
            ok.Click += OnOk;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = Strings.Get("ButtonCancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(311, 128);
            cancel.Size = new Size(75, 26);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            UpdateEnabled();
        }

        private void UpdateEnabled()
        {
            browseCopy.Enabled = !block.Checked;
            write.Enabled = !block.Checked;
        }

        /// <summary>Applies the form's choices to the entry, keeping the reserved permission bits.</summary>
        private void OnOk(object sender, EventArgs e)
        {
            entry.Decision = block.Checked ? TrustDecision.Deny : TrustDecision.AlwaysAllow;
            Permissions kept = entry.Permissions & ~TrustEntry.FileAccess;
            if (!block.Checked)
            {
                Permissions chosen = Permissions.None;
                if (browseCopy.Checked)
                {
                    chosen |= Permissions.Browse | Permissions.Read;
                }
                if (write.Checked)
                {
                    chosen |= Permissions.Write;
                }
                kept |= chosen;
            }
            else
            {
                kept |= entry.Permissions & TrustEntry.FileAccess; // remembered, so unblocking restores them
            }
            entry.Permissions = kept;
        }
    }
}
