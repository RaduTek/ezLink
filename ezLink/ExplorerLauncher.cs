using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    /// <summary>Starts ezExplorer.exe for the connected peer with the command line the plan defines.</summary>
    internal static class ExplorerLauncher
    {
        public static void Launch(LinkManager link, IntPtr mainWindow, IWin32Window owner)
        {
            PeerInfo peer = link.CurrentPeer;
            if (peer == null)
            {
                return;
            }

            string exe = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ezExplorer.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(owner, Strings.Get("ExplorerMissing"), Strings.Get("AppName"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string arguments = string.Format("/peer:{0} /port:{1} /uuid:{2} \"/name:{3}\" /main:{4}",
                peer.Address, peer.TcpPort, peer.MachineId, peer.ComputerName, mainWindow.ToInt64());
            Log.Info("Starting ezExplorer: {0}", arguments);
            try
            {
                Process.Start(new ProcessStartInfo(exe, arguments));
            }
            catch (Exception ex)
            {
                Log.Error("Could not start ezExplorer", ex);
                MessageBox.Show(owner, ex.Message, Strings.Get("AppName"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
