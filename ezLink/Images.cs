using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using EzLink;
using LinkState = EzLink.LinkState; // System.Windows.Forms has a LinkState too

namespace ezLink
{
    /// <summary>
    /// The picture to show for the link: disconnected, connecting (three animation frames), connected, active
    /// (a transfer is running) or error.
    /// </summary>
    internal enum LinkPicture
    {
        Disconnected,
        Connecting0,
        Connecting1,
        Connecting2,
        Connected,
        Active,
        Error
    }

    /// <summary>
    /// Maps the link state to its pictures. The large status images and toolbar images are project resources
    /// (Properties/Resources.resx); the tray icons are embedded PNGs in 16 px and 24 px variants, the larger one
    /// used when the system small icon size is 24 px or more (125% scaling and up).
    /// </summary>
    internal static class Images
    {
        public const int ConnectingFrames = 3;

        private static readonly Dictionary<string, Icon> trayIcons = new Dictionary<string, Icon>();

        /// <summary>
        /// True on high-DPI displays (120 DPI / 125% scaling and up), where the 24 px tray icons, 80 px status
        /// images and 40 px toolbar images are used. Read from the system DPI (needs the app to be DPI aware, see
        /// app.manifest) because SystemInformation.SmallIconSize does not follow the DPI on Windows XP and 7.
        /// </summary>
        public static bool HighDpi
        {
            get { return SystemDpi >= 120; }
        }

        private static int systemDpi;

        private static int SystemDpi
        {
            get
            {
                if (systemDpi == 0)
                {
                    IntPtr dc = NativeMethodsBridge.GetDC(IntPtr.Zero);
                    try
                    {
                        systemDpi = dc != IntPtr.Zero ? NativeMethodsBridge.GetDeviceCaps(dc, 88) : 96; // LOGPIXELSX
                    }
                    finally
                    {
                        if (dc != IntPtr.Zero)
                        {
                            NativeMethodsBridge.ReleaseDC(IntPtr.Zero, dc);
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

        public static LinkPicture Picture(LinkState state, bool transferActive, int animationFrame)
        {
            switch (state)
            {
                case LinkState.Searching:
                case LinkState.Connecting:
                    return (LinkPicture)((int)LinkPicture.Connecting0 + animationFrame % ConnectingFrames);
                case LinkState.Connected:
                    return transferActive ? LinkPicture.Active : LinkPicture.Connected;
                case LinkState.Error:
                    return LinkPicture.Error;
                default:
                    return LinkPicture.Disconnected; // no cable, or no link on the cable interface
            }
        }

        public static Bitmap Load(string name)
        {
            Assembly assembly = typeof(Images).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream("ezLink.Resources." + name + ".png"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Missing embedded image " + name);
                }
                using (Bitmap raw = new Bitmap(stream))
                {
                    return new Bitmap(raw); // detach from the stream
                }
            }
        }

        private static string Suffix(LinkPicture picture)
        {
            switch (picture)
            {
                case LinkPicture.Connecting0: return "connecting-0";
                case LinkPicture.Connecting1: return "connecting-1";
                case LinkPicture.Connecting2: return "connecting-2";
                case LinkPicture.Connected: return "connected";
                case LinkPicture.Active: return "active";
                case LinkPicture.Error: return "error";
                default: return "nocable";
            }
        }

        /// <summary>The large image for the status area, 64 or 80 px depending on the display.</summary>
        public static Image Status(LinkPicture picture)
        {
            switch (picture)
            {
                case LinkPicture.Connecting0: return HighDpi ? Properties.Resources.status_connecting_0_80 : Properties.Resources.status_connecting_0_64;
                case LinkPicture.Connecting1: return HighDpi ? Properties.Resources.status_connecting_1_80 : Properties.Resources.status_connecting_1_64;
                case LinkPicture.Connecting2: return HighDpi ? Properties.Resources.status_connecting_2_80 : Properties.Resources.status_connecting_2_64;
                case LinkPicture.Connected: return HighDpi ? Properties.Resources.status_connected_80 : Properties.Resources.status_connected_64;
                case LinkPicture.Active: return HighDpi ? Properties.Resources.status_active_80 : Properties.Resources.status_active_64;
                case LinkPicture.Error: return HighDpi ? Properties.Resources.status_error_80 : Properties.Resources.status_error_64;
                default: return HighDpi ? Properties.Resources.status_nocable_80 : Properties.Resources.status_nocable_64;
            }
        }

        /// <summary>The icon for the tray (and the window's title bar), 16 or 24 px depending on the display.</summary>
        public static Icon Tray(LinkPicture picture)
        {
            string name = "tray-" + Suffix(picture) + (HighDpi ? "-24" : string.Empty);
            Icon icon;
            if (!trayIcons.TryGetValue(name, out icon))
            {
                using (Bitmap bitmap = Load(name))
                {
                    IntPtr handle = bitmap.GetHicon();
                    try
                    {
                        icon = (Icon)Icon.FromHandle(handle).Clone(); // the clone owns its own copy
                    }
                    finally
                    {
                        NativeMethodsBridge.DestroyIcon(handle);
                    }
                }
                trayIcons[name] = icon;
            }
            return icon;
        }

        public static void DisposeAll()
        {
            foreach (Icon icon in trayIcons.Values)
            {
                icon.Dispose();
            }
            trayIcons.Clear();
        }
    }

    internal static class NativeMethodsBridge
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hdc, int index);
    }
}
