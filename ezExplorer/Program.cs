using System;
using System.Threading;
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

            ExplorerArgs parsed = ExplorerArgs.Parse(args);
            if (parsed == null)
            {
                MessageBox.Show(Strings.Get("ExplorerNoArguments"), Strings.Get("ExplorerTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // One explorer per peer: a second launch just brings the first one to the front.
            bool first;
            using (Mutex mutex = new Mutex(true, IpcMessages.ExplorerMutexName(parsed.PeerId), out first))
            {
                if (!first)
                {
                    ActivateRunningExplorer(parsed);
                    return;
                }

                using (IpcWindow ipc = new IpcWindow(IpcMessages.ExplorerWindowTitle(parsed.PeerId)))
                using (ExplorerForm form = new ExplorerForm(parsed, ipc))
                {
                    Application.Run(form);
                }
                GC.KeepAlive(mutex);
            }
        }

        private static void ActivateRunningExplorer(ExplorerArgs args)
        {
            using (IpcWindow self = new IpcWindow("ezLink.ExplorerLauncher.Ipc"))
            {
                // The first explorer may still be starting up: give its window a moment to appear.
                for (int attempt = 0; attempt < 15; attempt++)
                {
                    IntPtr target = IpcWindow.Find(IpcMessages.ExplorerWindowTitle(args.PeerId));
                    if (target != IntPtr.Zero)
                    {
                        IpcWindow.Send(target, self, IpcMessages.ActivateExplorer, string.Empty);
                        return;
                    }
                    Thread.Sleep(200);
                }
                Log.Warn("The running explorer did not answer");
            }
        }
    }
}
