using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    /// <summary>
    /// Starts ezExplorer.exe for the connected peer (or brings the one already open for that peer to the front),
    /// remembers the explorers' message windows, and tells them when the peer goes away or comes back.
    /// Use from the UI thread.
    /// </summary>
    internal sealed class ExplorerLauncher
    {
        private readonly IpcWindow ipc;
        private readonly Dictionary<Guid, IntPtr> explorers = new Dictionary<Guid, IntPtr>();

        public ExplorerLauncher(IpcWindow ipc)
        {
            this.ipc = ipc;
        }

        public void Launch(LinkManager link, IWin32Window owner)
        {
            PeerInfo peer = link.CurrentPeer;
            if (peer == null)
            {
                return;
            }

            IntPtr running = FindExplorer(peer.MachineId);
            if (running != IntPtr.Zero && IpcWindow.Send(running, ipc, IpcMessages.ActivateExplorer, string.Empty))
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

            // /self and /selfname are optional extras: they let a debug loopback instance say which machine it is.
            string arguments = string.Format("/peer:{0} /port:{1} /uuid:{2} \"/name:{3}\" /main:{4} /self:{5} \"/selfname:{6}\"",
                peer.Address, peer.TcpPort, peer.MachineId, peer.ComputerName, ipc.Handle.ToInt64(),
                link.MachineId, link.ComputerName);
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

        /// <summary>Handles ExplorerHello and ExplorerClosed; other message types are not for this class.</summary>
        public void OnMessage(IpcMessageEventArgs e)
        {
            Guid peer;
            switch (e.Type)
            {
                case IpcMessages.ExplorerHello:
                    if (TryParse(e.Payload, out peer))
                    {
                        explorers[peer] = e.Sender;
                    }
                    break;
                case IpcMessages.ExplorerClosed:
                    if (TryParse(e.Payload, out peer))
                    {
                        explorers.Remove(peer);
                    }
                    break;
            }
        }

        /// <summary>The control session to a peer ended: its explorer shows "Disconnected".</summary>
        public void PeerLost(Guid peerId)
        {
            IntPtr window = FindExplorer(peerId);
            if (window != IntPtr.Zero)
            {
                IpcWindow.Send(window, ipc, IpcMessages.PeerLost, peerId.ToString("D"));
            }
        }

        /// <summary>The peer is connected again, possibly at a new address: its explorer reconnects.</summary>
        public void PeerBack(PeerInfo peer)
        {
            IntPtr window = FindExplorer(peer.MachineId);
            if (window != IntPtr.Zero)
            {
                IpcWindow.Send(window, ipc, IpcMessages.PeerBack, peer.MachineId.ToString("D") + ";" + peer.Address);
            }
        }

        /// <summary>
        /// The explorer's message window: the one it announced, or else found by title (an explorer that was open
        /// before this ezLink started never announced itself to it).
        /// </summary>
        private IntPtr FindExplorer(Guid peerId)
        {
            IntPtr window;
            if (explorers.TryGetValue(peerId, out window))
            {
                return window;
            }
            window = IpcWindow.Find(IpcMessages.ExplorerWindowTitle(peerId));
            if (window != IntPtr.Zero)
            {
                explorers[peerId] = window;
            }
            return window;
        }

        private static bool TryParse(string text, out Guid id)
        {
            try
            {
                id = new Guid(text.Trim());
                return true;
            }
            catch (FormatException)
            {
                id = Guid.Empty;
                return false;
            }
            catch (OverflowException)
            {
                id = Guid.Empty;
                return false;
            }
        }
    }
}
