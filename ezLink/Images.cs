using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using EzLink;

namespace ezLink
{
    /// <summary>
    /// Loads the embedded PNGs (Oxygen icons, see THIRD-PARTY-NOTICES.md) and keeps one status image and one
    /// tray icon per link state.
    /// </summary>
    internal static class Images
    {
        private static readonly Dictionary<LinkState, Image> statusImages = new Dictionary<LinkState, Image>();
        private static readonly Dictionary<LinkState, Icon> trayIcons = new Dictionary<LinkState, Icon>();

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

        private static string Suffix(LinkState state)
        {
            switch (state)
            {
                case LinkState.NoCable: return "nocable";
                case LinkState.WaitingForNetwork: return "waiting";
                case LinkState.Searching: return "searching";
                case LinkState.Connecting: return "connecting";
                case LinkState.Connected: return "connected";
                default: return "error";
            }
        }

        /// <summary>The 64x64 image for the status area.</summary>
        public static Image Status(LinkState state)
        {
            Image image;
            if (!statusImages.TryGetValue(state, out image))
            {
                image = Load("status-" + Suffix(state));
                statusImages[state] = image;
            }
            return image;
        }

        /// <summary>The 16x16 icon for the tray (and the window's title bar).</summary>
        public static Icon Tray(LinkState state)
        {
            Icon icon;
            if (!trayIcons.TryGetValue(state, out icon))
            {
                using (Bitmap bitmap = Load("tray-" + Suffix(state)))
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
                trayIcons[state] = icon;
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
            foreach (Image image in statusImages.Values)
            {
                image.Dispose();
            }
            statusImages.Clear();
        }
    }

    internal static class NativeMethodsBridge
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
