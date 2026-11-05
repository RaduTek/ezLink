using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;
using EzLink.Protocol;

namespace ezLink
{
    /// <summary>Edits what one trusted computer may do on this computer, or blocks it.</summary>
    internal sealed partial class TrustEntryForm : Form
    {
        private readonly TrustEntry entry;

        public TrustEntryForm(TrustEntry entry)
        {
            this.entry = entry;

            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = string.Format(Strings.Get("TrustEditTitle"), entry.ComputerName);
            browseCopy.Text = Strings.Get("ApprovalBrowseCopy");
            write.Text = Strings.Get("ApprovalWrite");
            block.Text = Strings.Get("TrustEditBlock");
            okButton.Text = Strings.Get("ButtonOk");
            cancelButton.Text = Strings.Get("ButtonCancel");

            browseCopy.Checked = (entry.Permissions & (Permissions.Browse | Permissions.Read)) != 0;
            write.Checked = (entry.Permissions & Permissions.Write) != 0;
            block.Checked = entry.Decision == TrustDecision.Deny;
            UpdateEnabled();
        }

        private void block_CheckedChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
        }

        private void UpdateEnabled()
        {
            browseCopy.Enabled = !block.Checked;
            write.Enabled = !block.Checked;
        }

        /// <summary>Applies the form's choices to the entry, keeping the reserved permission bits.</summary>
        private void okButton_Click(object sender, EventArgs e)
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
