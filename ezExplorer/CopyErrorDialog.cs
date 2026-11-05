using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>Retry / Skip / Cancel after a file could not be copied.</summary>
    internal sealed class CopyErrorDialog : Form
    {
        private ErrorChoice choice = ErrorChoice.Cancel;

        public CopyErrorDialog(string name, string error)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = Strings.Get("TransferErrorTitle");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 160);

            Label message = new Label();
            message.Text = string.Format(Strings.Get("TransferErrorMessage"), name, error);
            message.SetBounds(12, 12, 416, 90);

            Button retry = MakeButton(Strings.Get("ButtonRetry"), ErrorChoice.Retry, 12);
            Button skip = MakeButton(Strings.Get("ButtonSkip"), ErrorChoice.Skip, 120);
            Button cancel = MakeButton(Strings.Get("ButtonCancel"), ErrorChoice.Cancel, 228);
            AcceptButton = retry;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { message, retry, skip, cancel });
        }

        private Button MakeButton(string text, ErrorChoice result, int left)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(left, 116, 100, 28);
            button.Click += delegate
            {
                choice = result;
                DialogResult = DialogResult.OK;
            };
            return button;
        }

        public ErrorChoice Choice
        {
            get { return choice; }
        }
    }
}
