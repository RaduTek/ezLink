using System;
using System.Security;
using Microsoft.Win32;

namespace EzLink
{
    /// <summary>
    /// This machine's UUID. The setup helper creates it in HKLM\SOFTWARE\ezLink\MachineId. If that is
    /// missing, ezLink falls back to a per-user value in HKCU\Software\ezLink\MachineId, creating it
    /// (with a logged warning) when needed.
    /// </summary>
    public static class MachineIdentity
    {
        public const string ValueName = "MachineId";
        public const string MachineKeyPath = @"SOFTWARE\" + Constants.AppFolderName;
        public const string UserKeyPath = @"Software\" + Constants.AppFolderName;

        private static readonly object sync = new object();
        private static Guid cached = Guid.Empty;

        public static Guid GetOrCreate()
        {
            lock (sync)
            {
                if (cached != Guid.Empty)
                {
                    return cached;
                }

                Guid id = Read(Registry.LocalMachine, MachineKeyPath);
                if (id != Guid.Empty)
                {
                    return cached = id;
                }

                id = Read(Registry.CurrentUser, UserKeyPath);
                if (id != Guid.Empty)
                {
                    Log.Warn("Machine UUID taken from HKCU; run the setup helper to create it machine-wide");
                    return cached = id;
                }

                id = Guid.NewGuid();
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UserKeyPath))
                    {
                        key.SetValue(ValueName, id.ToString("D"));
                    }
                    Log.Warn("No machine UUID found; generated {0} in HKCU. Run the setup helper to create it machine-wide.", id);
                }
                catch (Exception ex)
                {
                    Log.Error("Could not store the generated machine UUID; it will change on restart", ex);
                }
                return cached = id;
            }
        }

        private static Guid Read(RegistryKey root, string path)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(path))
                {
                    string text = key == null ? null : key.GetValue(ValueName) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return new Guid(text);
                    }
                }
            }
            catch (FormatException)
            {
                Log.Warn("Ignoring malformed MachineId in {0}\\{1}", root.Name, path);
            }
            catch (SecurityException)
            {
            }
            return Guid.Empty;
        }
    }
}
