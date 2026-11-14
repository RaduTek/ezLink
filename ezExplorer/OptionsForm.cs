using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>Explorer options: whether hidden and system files are listed.</summary>
    internal sealed partial class OptionsForm : Form
    {
        public OptionsForm(bool showHidden, bool showSystem)
        {
            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = Strings.Get("OptionsTitle");
            showHiddenCheck.Text = Strings.Get("OptionsShowHidden");
            showSystemCheck.Text = Strings.Get("OptionsShowSystem");
            okButton.Text = Strings.Get("ButtonOk");
            cancelButton.Text = Strings.Get("ButtonCancel");

            showHiddenCheck.Checked = showHidden;
            showSystemCheck.Checked = showSystem;
        }

        public bool ShowHidden
        {
            get { return showHiddenCheck.Checked; }
        }

        public bool ShowSystem
        {
            get { return showSystemCheck.Checked; }
        }
    }
}
