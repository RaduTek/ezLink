using System;
using System.Collections.Generic;
using EzLink.Protocol;
using Microsoft.Win32;

namespace EzLink
{
    /// <summary>
    /// What this computer lets the other one do and see: the "allow all connections" bypass, the file access it
    /// grants, how hidden / system / protected files are treated and which folders and drives are shared.
    /// One live instance is shared by the trust manager and every file server, so a change in Options applies at
    /// once. Kept per user in HKCU; failures are logged and the defaults are used.
    /// </summary>
    public sealed class SharingSettings
    {
        private const string KeyPath = @"Software\" + Constants.AppFolderName + @"\Settings";

        /// <summary>Skip the Trusted Devices mechanism: everyone gets <see cref="AllowedPermissions"/>.</summary>
        public volatile bool AllowAllConnections;
        /// <summary>The "Browse and copy" box: the default of the trust prompt and the access of allow-all.</summary>
        public volatile bool AllowBrowseCopy = true;
        /// <summary>The "Write and delete" box.</summary>
        public volatile bool AllowWrite;
        /// <summary>Show hidden and system files and protected folders to the peer.</summary>
        public volatile bool ShowHiddenAndSystem;
        /// <summary>Refuse writes, deletes and renames in protected folders and of system files.</summary>
        public volatile bool ProtectSystemFiles = true;
        public volatile bool ShareUserProfile = true;
        public volatile bool ShareFixedDrives;
        public volatile bool ShareRemovableDrives;
        /// <summary>Bring the ezLink window forward when a computer connects (every time it connects).</summary>
        public volatile bool ShowWindowOnConnect;
        /// <summary>Open an explorer for the computer that connects (every time it connects).</summary>
        public volatile bool OpenExplorerOnConnect;
        public volatile bool FixedDrivesReadOnly;
        public volatile bool RemovableDrivesReadOnly;

        private readonly object foldersLock = new object();
        private List<SharedFolder> folders = new List<SharedFolder>();

        /// <summary>Raised after <see cref="Save"/> (on the caller's thread).</summary>
        public event EventHandler Changed;

        /// <summary>A copy of the extra shared folders and drives (safe to enumerate on any thread).</summary>
        public List<SharedFolder> GetFolders()
        {
            lock (foldersLock)
            {
                List<SharedFolder> copy = new List<SharedFolder>(folders.Count);
                foreach (SharedFolder folder in folders)
                {
                    copy.Add(folder.Clone());
                }
                return copy;
            }
        }

        public void SetFolders(List<SharedFolder> list)
        {
            List<SharedFolder> copy = new List<SharedFolder>(list.Count);
            foreach (SharedFolder folder in list)
            {
                copy.Add(folder.Clone());
            }
            lock (foldersLock)
            {
                folders = copy;
            }
        }

        /// <summary>The permissions the two file boxes stand for.</summary>
        public Permissions AllowedPermissions
        {
            get
            {
                Permissions p = Permissions.None;
                if (AllowBrowseCopy)
                {
                    p |= Permissions.Browse | Permissions.Read;
                }
                if (AllowWrite)
                {
                    p |= Permissions.Write;
                }
                return p;
            }
        }

        /// <summary>Everything shared, nothing hidden or protected: what the file server did before these options.</summary>
        public static SharingSettings Permissive()
        {
            SharingSettings s = new SharingSettings();
            s.ShowHiddenAndSystem = true;
            s.ProtectSystemFiles = false;
            s.ShareUserProfile = true;
            s.ShareFixedDrives = true;
            s.ShareRemovableDrives = true;
            return s;
        }

        public static SharingSettings Load()
        {
            SharingSettings s = new SharingSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (key == null)
                    {
                        return s;
                    }
                    s.AllowAllConnections = Read(key, "AllowAllConnections", s.AllowAllConnections);
                    s.AllowBrowseCopy = Read(key, "AllowBrowseCopy", s.AllowBrowseCopy);
                    s.AllowWrite = Read(key, "AllowWrite", s.AllowWrite);
                    s.ShowHiddenAndSystem = Read(key, "ShowHiddenAndSystem", s.ShowHiddenAndSystem);
                    s.ProtectSystemFiles = Read(key, "ProtectSystemFiles", s.ProtectSystemFiles);
                    s.ShareUserProfile = Read(key, "ShareUserProfile", s.ShareUserProfile);
                    s.ShareFixedDrives = Read(key, "ShareFixedDrives", s.ShareFixedDrives);
                    s.ShareRemovableDrives = Read(key, "ShareRemovableDrives", s.ShareRemovableDrives);
                    s.ShowWindowOnConnect = Read(key, "ShowWindowOnConnect", s.ShowWindowOnConnect);
                    s.OpenExplorerOnConnect = Read(key, "OpenExplorerOnConnect", s.OpenExplorerOnConnect);
                    s.FixedDrivesReadOnly = Read(key, "FixedDrivesReadOnly", s.FixedDrivesReadOnly);
                    s.RemovableDrivesReadOnly = Read(key, "RemovableDrivesReadOnly", s.RemovableDrivesReadOnly);
                    s.SetFolders(SharedFolder.ParseAll(key.GetValue("Folders") as string[]));
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the settings: {0}", ex.Message);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    Write(key, "AllowAllConnections", AllowAllConnections);
                    Write(key, "AllowBrowseCopy", AllowBrowseCopy);
                    Write(key, "AllowWrite", AllowWrite);
                    Write(key, "ShowHiddenAndSystem", ShowHiddenAndSystem);
                    Write(key, "ProtectSystemFiles", ProtectSystemFiles);
                    Write(key, "ShareUserProfile", ShareUserProfile);
                    Write(key, "ShareFixedDrives", ShareFixedDrives);
                    Write(key, "ShareRemovableDrives", ShareRemovableDrives);
                    Write(key, "ShowWindowOnConnect", ShowWindowOnConnect);
                    Write(key, "OpenExplorerOnConnect", OpenExplorerOnConnect);
                    Write(key, "FixedDrivesReadOnly", FixedDrivesReadOnly);
                    Write(key, "RemovableDrivesReadOnly", RemovableDrivesReadOnly);
                    key.SetValue("Folders", SharedFolder.FormatAll(GetFolders()), RegistryValueKind.MultiString);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save the settings: {0}", ex.Message);
            }
            EventHandler handler = Changed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static bool Read(RegistryKey key, string name, bool fallback)
        {
            object value = key.GetValue(name);
            return value is int ? (int)value != 0 : fallback;
        }

        private static void Write(RegistryKey key, string name, bool value)
        {
            key.SetValue(name, value ? 1 : 0);
        }
    }
}
