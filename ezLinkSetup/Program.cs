using System;
using System.Windows.Forms;
using EzLink;

namespace ezLinkSetup
{
    /// <summary>
    /// Admin-only helper (requireAdministrator manifest). The verbs
    /// (/install, /uuid, /firewall, ...) are implemented in milestone 4.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppBootstrap.Start("ezLinkSetup", args);

            if (args.Length == 0)
            {
                MessageBox.Show(Strings.Get("SetupPlaceholder"), Strings.Get("SetupTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log.Warn("No verbs implemented yet; ignoring arguments");
            }
            return 0;
        }
    }
}
