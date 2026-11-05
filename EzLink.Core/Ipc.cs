using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EzLink
{
    /// <summary>The WM_COPYDATA message types (dwData) exchanged between ezLink.exe and ezExplorer.exe.</summary>
    public static class IpcMessages
    {
        /// <summary>Any launch to the running ezLink.exe: bring the main window to the front.</summary>
        public const int ShowWindow = 1;
        /// <summary>ezLink.exe to ezExplorer.exe: bring the explorer window to the front.</summary>
        public const int ActivateExplorer = 2;
        /// <summary>ezLink.exe to ezExplorer.exe; payload: peer UUID.</summary>
        public const int PeerLost = 3;
        /// <summary>ezLink.exe to ezExplorer.exe; payload: peer UUID, new IP.</summary>
        public const int PeerBack = 4;
        /// <summary>ezExplorer.exe to ezLink.exe; payload: peer UUID.</summary>
        public const int ExplorerClosed = 5;
        /// <summary>ezExplorer.exe to ezLink.exe right after start; payload: peer UUID.</summary>
        public const int ExplorerHello = 6;

        /// <summary>The title of an explorer's message window; ezLink.exe finds it by the peer it browses.</summary>
        public static string ExplorerWindowTitle(Guid peerId)
        {
            return "ezLink.Explorer.Ipc." + peerId.ToString("D");
        }

        /// <summary>The name of the mutex that keeps it to one explorer per peer.</summary>
        public static string ExplorerMutexName(Guid peerId)
        {
            return @"Local\ezLink.Explorer." + peerId.ToString("D");
        }
    }

    public sealed class IpcMessageEventArgs : EventArgs
    {
        public readonly int Type;
        /// <summary>The UTF-8 payload (may be empty).</summary>
        public readonly string Payload;
        /// <summary>The sender's message window.</summary>
        public readonly IntPtr Sender;

        public IpcMessageEventArgs(int type, string payload, IntPtr sender)
        {
            Type = type;
            Payload = payload;
            Sender = sender;
        }
    }

    /// <summary>
    /// A hidden message-only window that receives WM_COPYDATA notifications from the other ezLink processes.
    /// Create it on the UI thread. Senders whose process is not ezLink.exe or ezExplorer.exe are ignored, as are
    /// message types the receiver does not handle.
    /// </summary>
    public sealed class IpcWindow : NativeWindow, IDisposable
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Raised on the thread that created the window.</summary>
        public event EventHandler<IpcMessageEventArgs> MessageReceived;

        /// <param name="title">The window text other processes use to find this window.</param>
        public IpcWindow(string title)
        {
            CreateParams cp = new CreateParams();
            cp.Caption = title;
            cp.Parent = NativeMethods.HWND_MESSAGE;
            CreateHandle(cp);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }

        /// <summary>Finds another process's message window by its title; IntPtr.Zero if there is none.</summary>
        public static IntPtr Find(string title)
        {
            return NativeMethods.FindWindowEx(NativeMethods.HWND_MESSAGE, IntPtr.Zero, null, title);
        }

        /// <summary>
        /// Sends a notification. <paramref name="from"/> is this process's own message window (it identifies the
        /// sender to the receiver). Returns false if the target did not answer within two seconds.
        /// </summary>
        public static bool Send(IntPtr target, IpcWindow from, int type, string payload)
        {
            byte[] bytes = Utf8.GetBytes(payload == null ? string.Empty : payload);
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length == 0 ? 1 : bytes.Length);
            IntPtr structure = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.COPYDATASTRUCT)));
            try
            {
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                NativeMethods.COPYDATASTRUCT data = new NativeMethods.COPYDATASTRUCT();
                data.dwData = new IntPtr(type);
                data.cbData = bytes.Length;
                data.lpData = buffer;
                Marshal.StructureToPtr(data, structure, false);

                IntPtr result;
                IntPtr ok = NativeMethods.SendMessageTimeout(target, NativeMethods.WM_COPYDATA, from.Handle,
                    structure, NativeMethods.SMTO_ABORTIFHUNG, 2000, out result);
                return ok != IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(structure);
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>True if the window belongs to an ezLink.exe or ezExplorer.exe process.</summary>
        public static bool IsEzLinkProcess(IntPtr window)
        {
            try
            {
                uint pid;
                NativeMethods.GetWindowThreadProcessId(window, out pid);
                if (pid == 0)
                {
                    return false;
                }
                string name = Process.GetProcessById((int)pid).ProcessName;
                return string.Equals(name, "ezLink", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "ezExplorer", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("ezLink.", StringComparison.OrdinalIgnoreCase)       // e.g. the VS host process
                    || name.StartsWith("ezExplorer.", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false; // the process has already exited
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_COPYDATA)
            {
                HandleCopyData(ref m);
                return;
            }
            base.WndProc(ref m);
        }

        private void HandleCopyData(ref Message m)
        {
            IntPtr sender = m.WParam;
            if (!IsEzLinkProcess(sender))
            {
                Log.Warn("Ignored a WM_COPYDATA from a foreign process");
                m.Result = IntPtr.Zero;
                return;
            }

            NativeMethods.COPYDATASTRUCT data =
                (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.COPYDATASTRUCT));
            if (data.cbData < 0 || data.cbData > 4096)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            string payload = string.Empty;
            if (data.cbData > 0 && data.lpData != IntPtr.Zero)
            {
                byte[] bytes = new byte[data.cbData];
                Marshal.Copy(data.lpData, bytes, 0, bytes.Length);
                payload = Utf8.GetString(bytes);
            }

            EventHandler<IpcMessageEventArgs> handler = MessageReceived;
            if (handler != null)
            {
                handler(this, new IpcMessageEventArgs(data.dwData.ToInt32(), payload, sender));
            }
            m.Result = new IntPtr(1);
        }
    }
}
