using System;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            AppBootstrap.Start("ezExplorer", args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ExplorerForm());
        }
    }
}
