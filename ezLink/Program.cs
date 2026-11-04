using System;
using System.Reflection;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string processName = "ezLink";
#if DEBUG
            int loopback = LoopbackAdapterSource.Parse(args);
            if (loopback != 0)
            {
                processName = "ezLink-loop" + loopback; // separate log file per instance
            }
#endif
            AppBootstrap.Start(processName, args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Until the connection state machine arrives (milestone 5) discovery just logs what it finds.
            DiscoveryService discovery = CreateDiscovery(args);
            discovery.Start();
            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                discovery.Stop();
            }
        }

        private static DiscoveryService CreateDiscovery(string[] args)
        {
            IAdapterSource source = new SystemAdapterSource();
            Guid machineId = MachineIdentity.GetOrCreate();
            string computerName = Environment.MachineName;
            ushort sessionPort = Constants.SessionPort;
            DiscoveryOptions options = new DiscoveryOptions();

#if DEBUG
            int loopback = LoopbackAdapterSource.Parse(args);
            if (loopback != 0)
            {
                source = new LoopbackAdapterSource(loopback);
                machineId = LoopbackAdapterSource.IdFor(loopback);
                computerName = computerName + "-loop" + loopback;
                sessionPort = (ushort)(Constants.SessionPort + (loopback - 1) * 2);
                options.LocalPort = LoopbackAdapterSource.PortFor(loopback);
                options.RemotePort = LoopbackAdapterSource.OtherPortFor(loopback);
                Log.Warn("DEBUG loopback peer {0} active: UDP {1} -> {2}", loopback, options.LocalPort, options.RemotePort);
            }
#endif

            string appVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(3);
            return new DiscoveryService(source, machineId, computerName, appVersion, sessionPort, options);
        }
    }
}
