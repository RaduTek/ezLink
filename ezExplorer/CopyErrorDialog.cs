using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>Retry / Skip / Cancel after a file could not be copied.</summary>
    internal sealed partial class CopyErrorDialog : Form
    {
        private ErrorChoice choice = ErrorChoice.Cancel;

        public CopyErrorDialog(string name, string error)
        {
            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = Strings.Get("TransferErrorTitle");
            message.Text = string.Format(Strings.Get("TransferErrorMessage"), name, error);
            retryButton.Text = Strings.Get("ButtonRetry");
            skipButton.Text = Strings.Get("ButtonSkip");
            cancelButton.Text = Strings.Get("ButtonCancel");
        }

        private void retryButton_Click(object sender, EventArgs e)
        {
            Choose(ErrorChoice.Retry);
        }

        private void skipButton_Click(object sender, EventArgs e)
        {
            Choose(ErrorChoice.Skip);
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Choose(ErrorChoice.Cancel);
        }

        private void Choose(ErrorChoice result)
        {
            choice = result;
            DialogResult = DialogResult.OK;
        }

        public ErrorChoice Choice
        {
            get { return choice; }
        }
    }
}
