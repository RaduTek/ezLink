using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>Replace / Skip / Keep both, with "apply to all". A child of the transfer dialog.</summary>
    internal sealed class ConflictDialog : Form
    {
        private readonly CheckBox applyAll = new CheckBox();
        private ConflictChoice choice = ConflictChoice.Skip;

        public ConflictDialog(string name, long existingSize, DateTime existingModified, long newSize,
            DateTime newModified)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = Strings.Get("ConflictTitle");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 190);

            Label message = new Label();
            message.Text = string.Format(Strings.Get("ConflictMessage"), name);
            message.SetBounds(12, 12, 416, 36);
            message.AutoEllipsis = true;

            Label existing = new Label();
            existing.Text = string.Format(Strings.Get("ConflictExisting"), RemoteListView.FormatSize(existingSize),
                existingModified.ToLocalTime().ToString("g"));
            existing.SetBounds(12, 52, 416, 20);

            Label incoming = new Label();
            incoming.Text = string.Format(Strings.Get("ConflictIncoming"), RemoteListView.FormatSize(newSize),
                newModified.ToLocalTime().ToString("g"));
            incoming.SetBounds(12, 74, 416, 20);

            applyAll.Text = Strings.Get("ConflictApplyAll");
            applyAll.SetBounds(12, 104, 416, 22);

            Button replace = MakeButton(Strings.Get("ConflictReplace"), ConflictChoice.Replace, 12);
            Button keep = MakeButton(Strings.Get("ConflictKeepBoth"), ConflictChoice.KeepBoth, 120);
            Button skip = MakeButton(Strings.Get("ConflictSkip"), ConflictChoice.Skip, 228);
            AcceptButton = replace;
            CancelButton = skip;

            Controls.AddRange(new Control[] { message, existing, incoming, applyAll, replace, keep, skip });
        }

        private Button MakeButton(string text, ConflictChoice result, int left)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(left, 142, 100, 28);
            button.Click += delegate
            {
                choice = result;
                DialogResult = DialogResult.OK;
            };
            return button;
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
