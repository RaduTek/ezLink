using System;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            AppBootstrap.Start("ezLink", args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
