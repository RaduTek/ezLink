using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;
using Microsoft.Win32;

namespace ezExplorer
{
    /// <summary>Per-user window preferences (size, splitter position, view), kept in HKCU. Failures are ignored.</summary>
    internal sealed class ExplorerSettings
    {
        private const string KeyPath = @"Software\" + Constants.AppFolderName + @"\Explorer";

        public Size WindowSize = new Size(900, 600);
        public bool Maximized;
        public int SplitterDistance = 240;
        public View View = View.Details;

        public static ExplorerSettings Load()
        {
            ExplorerSettings settings = new ExplorerSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (key == null)
                    {
                        return settings;
                    }
                    int width = ReadInt(key, "Width", settings.WindowSize.Width);
                    int height = ReadInt(key, "Height", settings.WindowSize.Height);
                    if (width >= 400 && height >= 300 && width <= 10000 && height <= 10000)
                    {
                        settings.WindowSize = new Size(width, height);
                    }
                    settings.Maximized = ReadInt(key, "Maximized", 0) != 0;
                    int split = ReadInt(key, "SplitterDistance", settings.SplitterDistance);
                    if (split >= 50 && split <= 4000)
                    {
                        settings.SplitterDistance = split;
                    }
                    int view = ReadInt(key, "View", (int)View.Details);
                    if (view == (int)View.Details || view == (int)View.LargeIcon || view == (int)View.List)
                    {
                        settings.View = (View)view;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the explorer settings: {0}", ex.Message);
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    key.SetValue("Width", WindowSize.Width);
                    key.SetValue("Height", WindowSize.Height);
                    key.SetValue("Maximized", Maximized ? 1 : 0);
                    key.SetValue("SplitterDistance", SplitterDistance);
                    key.SetValue("View", (int)View);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save the explorer settings: {0}", ex.Message);
            }
        }

        private static int ReadInt(RegistryKey key, string name, int fallback)
        {
            object value = key.GetValue(name);
            return value is int ? (int)value : fallback;
        }
    }
}
