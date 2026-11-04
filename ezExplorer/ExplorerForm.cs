using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>Placeholder window; the remote pane is milestone 8.</summary>
    internal class ExplorerForm : Form
    {
        public ExplorerForm()
        {
            Text = Strings.Get("ExplorerTitle");
            ClientSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterScreen;

            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Text = Strings.Get("ExplorerPlaceholder");
            Controls.Add(label);
        }
    }
}
