using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    /// <summary>Placeholder window; the ActiveSync-style status window is milestone 5.</summary>
    internal class MainForm : Form
    {
        public MainForm()
        {
            Text = Strings.Get("AppName");
            ClientSize = new Size(480, 320);
            StartPosition = FormStartPosition.CenterScreen;

            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Text = Strings.Get("MainPlaceholder");
            Controls.Add(label);
        }
    }
}
