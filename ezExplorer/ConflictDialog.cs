using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>Replace / Skip / Keep both, with "apply to all". A child of the transfer dialog.</summary>
    internal sealed partial class ConflictDialog : Form
    {
        private ConflictChoice choice = ConflictChoice.Skip;

        public ConflictDialog(string name, long existingSize, DateTime existingModified, long newSize,
            DateTime newModified)
        {
            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = Strings.Get("ConflictTitle");
            message.Text = string.Format(Strings.Get("ConflictMessage"), name);
            existing.Text = string.Format(Strings.Get("ConflictExisting"), RemoteListView.FormatSize(existingSize),
                existingModified.ToLocalTime().ToString("g"));
            incoming.Text = string.Format(Strings.Get("ConflictIncoming"), RemoteListView.FormatSize(newSize),
                newModified.ToLocalTime().ToString("g"));
            applyAll.Text = Strings.Get("ConflictApplyAll");
            replaceButton.Text = Strings.Get("ConflictReplace");
            keepButton.Text = Strings.Get("ConflictKeepBoth");
            skipButton.Text = Strings.Get("ConflictSkip");
        }

        private void replaceButton_Click(object sender, EventArgs e)
        {
            Choose(ConflictChoice.Replace);
        }

        private void keepButton_Click(object sender, EventArgs e)
        {
            Choose(ConflictChoice.KeepBoth);
        }

        private void skipButton_Click(object sender, EventArgs e)
        {
            Choose(ConflictChoice.Skip);
        }

        private void Choose(ConflictChoice result)
        {
            choice = result;
            DialogResult = DialogResult.OK;
        }

        public ConflictChoice Choice
        {
            get { return choice; }
        }

        public bool ApplyToAll
        {
            get { return applyAll.Checked; }
        }
    }
}
