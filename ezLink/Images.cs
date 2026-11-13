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

        /// <summary>True on high-DPI displays, where the 24 px tray icons and 48 px toolbar images are used.</summary>
        public static bool HighDpi
        {
            get { return SystemInformation.SmallIconSize.Width >= 24; }
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

        /// <summary>The large image for the status area.</summary>
        public static Image Status(LinkPicture picture)
        {
            switch (picture)
            {
                case LinkPicture.Connecting0: return Properties.Resources.status_connecting_0;
                case LinkPicture.Connecting1: return Properties.Resources.status_connecting_1;
                case LinkPicture.Connecting2: return Properties.Resources.status_connecting_2;
                case LinkPicture.Connected: return Properties.Resources.status_connected;
                case LinkPicture.Active: return Properties.Resources.status_active;
                case LinkPicture.Error: return Properties.Resources.status_error;
                default: return Properties.Resources.status_nocable;
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
    }
}
