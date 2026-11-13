using System;
using System.Runtime.InteropServices;

namespace ezExplorer
{
    /// <summary>
    /// The system DPI, read from the screen device context. SystemInformation.SmallIconSize does not follow the
    /// DPI on Windows XP and 7, and the app must be DPI aware (see app.manifest) or Windows reports 96.
    /// </summary>
    internal static class DpiInfo
    {
        private const int LOGPIXELSX = 88;

        private static int systemDpi;

        public static int SystemDpi
        {
            get
            {
                if (systemDpi == 0)
                {
                    IntPtr dc = GetDC(IntPtr.Zero);
                    try
                    {
                        systemDpi = dc != IntPtr.Zero ? GetDeviceCaps(dc, LOGPIXELSX) : 96;
                    }
                    finally
                    {
                        if (dc != IntPtr.Zero)
                        {
                            ReleaseDC(IntPtr.Zero, dc);
                        }
                    }
                    if (systemDpi <= 0)
                    {
                        systemDpi = 96;
                    }
                }
                return systemDpi;
            }
        }

        /// <summary>True at 120 DPI (125% scaling) and up, where the larger toolbar images are used.</summary>
        public static bool HighDpi
        {
            get { return SystemDpi >= 120; }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int index);
    }
}
