using System;
using System.Runtime.InteropServices;

namespace EzLink
{
    /// <summary>
    /// Win32 declarations. Handles and pointers are IntPtr (never int) so the signatures are correct for both
    /// 32- and 64-bit processes.
    /// </summary>
    internal static class NativeMethods
    {
        public const int WM_COPYDATA = 0x004A;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;   // ULONG_PTR
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam,
            uint flags, uint timeoutMs, out IntPtr result);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
