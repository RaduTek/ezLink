using System.Windows.Forms;
using Microsoft.Win32;

namespace ezLink
{
    /// <summary>Start at logon via HKCU\Software\Microsoft\Windows\CurrentVersion\Run (no elevation needed).</summary>
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ezLink";

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
        }

        public static void Set(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                {
                    // /minimized: at logon the program starts in the tray instead of opening its window.
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" /minimized");
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
