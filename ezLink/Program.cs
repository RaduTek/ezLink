using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    internal static class Program
    {
        private const string MutexName = @"Local\ezLink.Main";
        private const string MainWindowTitle = "ezLink.Main.Ipc";

        [STAThread]
        private static void Main(string[] args)
        {
            string suffix = string.Empty; // lets the debug loopback instances run side by side
#if DEBUG
            int loopback = LoopbackAdapterSource.Parse(args);
            if (loopback != 0)
            {
                suffix = "-loop" + loopback;
            }
#endif
            AppBootstrap.Start("ezLink" + suffix, args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool first;
            using (Mutex mutex = new Mutex(true, MutexName + suffix, out first))
            {
                if (!first)
                {
                    // One instance per user session: ask the running one to show its window, then leave.
                    ShowRunningInstance(MainWindowTitle + suffix);
                    return;
                }

                bool startHidden = Array.IndexOf(args, "/minimized") >= 0;
                WinFormsApprovalPrompt prompt = new WinFormsApprovalPrompt();
                LinkManager link = CreateLink(args, suffix, prompt);
                try
                {
                    using (IpcWindow ipc = new IpcWindow(MainWindowTitle + suffix))
                    using (MainForm form = new MainForm(link, ipc, startHidden))
                    {
                        prompt.Attach(form);
                        link.Start();
                        Application.Run(form);
                    }
                }
                finally
                {
                    link.Dispose();
                    Images.DisposeAll();
                }
                GC.KeepAlive(mutex);
            }
        }

        private static void ShowRunningInstance(string title)
        {
            using (IpcWindow self = new IpcWindow("ezLink.Launcher.Ipc"))
            {
                // The first instance may still be starting up: give its window a moment to appear.
                for (int attempt = 0; attempt < 15; attempt++)
                {
                    IntPtr target = IpcWindow.Find(title);
                    if (target != IntPtr.Zero)
                    {
                        IpcWindow.Send(target, self, IpcMessages.ShowWindow, string.Empty);
                        return;
                    }
                    Thread.Sleep(200);
                }
                Log.Warn("The running ezLink did not answer");
            }
        }

        private static LinkManager CreateLink(string[] args, string suffix, WinFormsApprovalPrompt prompt)
        {
            IAdapterSource source = new SystemAdapterSource();
            Guid machineId = MachineIdentity.GetOrCreate();
            string computerName = Environment.MachineName;
            LinkOptions options = new LinkOptions();

#if DEBUG
            int loopback = LoopbackAdapterSource.Parse(args);
            if (loopback != 0)
            {
                source = new LoopbackAdapterSource(loopback);
                machineId = LoopbackAdapterSource.IdFor(loopback);
                computerName = computerName + "-loop" + loopback;
                options.ListenPort = (ushort)(Constants.SessionPort + (loopback - 1) * 2);
                options.Discovery.LocalPort = LoopbackAdapterSource.PortFor(loopback);
                options.Discovery.RemotePort = LoopbackAdapterSource.OtherPortFor(loopback);
                Log.Warn("DEBUG loopback peer {0} active: UDP {1} -> {2}, TCP {3}", loopback,
                    options.Discovery.LocalPort, options.Discovery.RemotePort, options.ListenPort);
            }
#endif

            string appVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(3);
            TrustManager trust = new TrustManager(new TrustStore(TrustStore.DefaultPath(suffix)), prompt);
            return new LinkManager(source, machineId, computerName, appVersion, options, trust);
        }
    }
}
