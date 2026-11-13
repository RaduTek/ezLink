using System;
using Microsoft.Win32;
using EzLink;

namespace ezLinkSetup
{
    /// <summary>The /uuid and /regenerate-uuid verbs: HKLM\SOFTWARE\ezLink\MachineId.</summary>
    internal static class MachineIdSetup
    {
        /// <summary>Creates the machine UUID if it is missing or malformed; an existing valid one is left alone.</summary>
        public static void Ensure()
        {
            Guid existing = Read();
            if (existing != Guid.Empty)
            {
                SetupLog.Info("Machine UUID already present ({0}); unchanged", existing);
                return;
            }
            Guid id = Write(Guid.NewGuid());
            SetupLog.Info("Machine UUID created: {0}", id);
        }

        /// <summary>Replaces the machine UUID with a new one (the cloned-disk-image fix).</summary>
        public static void Regenerate()
        {
            Guid old = Read();
            Guid id = Write(Guid.NewGuid());
            SetupLog.Info("Machine UUID regenerated: {0} -> {1}", old, id);
        }

        /// <summary>Deletes the machine UUID (only for /uninstall /purge).</summary>
        public static void Remove()
        {
            using (RegistryKey software = Registry.LocalMachine.OpenSubKey("SOFTWARE", true))
            {
                // .NET 2.0 has no throwOnMissing overload, so check first.
                if (software != null)
                {
                    RegistryKey existing = software.OpenSubKey(Constants.AppFolderName);
                    if (existing != null)
                    {
                        existing.Close();
                        software.DeleteSubKeyTree(Constants.AppFolderName);
                    }
                }
            }
            SetupLog.Info("Machine UUID removed");
        }

        private static Guid Read()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(MachineIdentity.MachineKeyPath))
            {
                string text = key == null ? null : key.GetValue(MachineIdentity.ValueName) as string;
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        return new Guid(text);
                    }
                    catch (FormatException)
                    {
                        SetupLog.Info("The stored machine UUID is malformed and will be replaced");
                    }
                }
            }
            return Guid.Empty;
        }

        private static Guid Write(Guid id)
        {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(MachineIdentity.MachineKeyPath))
            {
                key.SetValue(MachineIdentity.ValueName, id.ToString("D"));
            }
            return id;
        }
    }
}
