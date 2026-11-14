using System;
using System.Collections.Generic;
using System.IO;

namespace EzLink
{
    /// <summary>
    /// The rules for what the peer may see and change: which folders are shared, which folders are protected
    /// system locations, and the user profile folder that the peer addresses as "~".
    /// Built only from APIs that exist on .NET 2.0 / Windows XP.
    /// </summary>
    public static class PathPolicy
    {
        private static readonly object sync = new object();
        private static string profileOverride;
        private static string[] protectedFolders;

        /// <summary>Names at the root of every drive that hold system data.</summary>
        private static readonly string[] AnyDriveNames = new string[]
        {
            "$Recycle.Bin", "RECYCLER", "System Volume Information"
        };

        /// <summary>Names at the root of the system drive only (on other drives they may be the user's own folders).</summary>
        private static readonly string[] SystemDriveNames = new string[]
        {
            "Recovery", "Config.Msi", "Boot", "PerfLogs", "Windows.old", "$WinREAgent"
        };

        private static readonly string[] RootFiles = new string[]
        {
            "pagefile.sys", "hiberfil.sys", "swapfile.sys"
        };

        /// <summary>The full path of the shared user profile folder, without a trailing backslash.</summary>
        public static string ProfileFolder
        {
            get
            {
                lock (sync)
                {
                    if (profileOverride != null)
                    {
                        return profileOverride;
                    }
                }
                // SpecialFolder.UserProfile does not exist on .NET 2.0.
                string profile = Environment.GetEnvironmentVariable("USERPROFILE");
                if (string.IsNullOrEmpty(profile))
                {
                    profile = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    string parent = string.IsNullOrEmpty(profile) ? null : Path.GetDirectoryName(profile);
                    profile = parent != null ? parent : profile;
                }
                return TrimEnd(profile);
            }
        }

        /// <summary>For tests: use this folder as the profile (null restores the real one).</summary>
        public static void SetProfileFolder(string folder)
        {
            lock (sync)
            {
                profileOverride = folder == null ? null : TrimEnd(folder);
                protectedFolders = null;
            }
        }

        /// <summary>The name shown for the profile root ("Ana").</summary>
        public static string ProfileName
        {
            get
            {
                string folder = ProfileFolder;
                string name = Path.GetFileName(folder);
                return string.IsNullOrEmpty(name) ? folder : name;
            }
        }

        public static bool IsDriveTypeShared(DriveType type, SharingSettings settings)
        {
            if (type == DriveType.Fixed)
            {
                return settings.ShareFixedDrives;
            }
            return type == DriveType.Removable && settings.ShareRemovableDrives;
        }

        /// <summary>True if the path is, or lies inside, a protected system location.</summary>
        public static bool IsProtected(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || fullPath.Length < 3)
            {
                return false;
            }
            foreach (string folder in ProtectedFolders())
            {
                if (IsInside(fullPath, folder))
                {
                    return true;
                }
            }

            // Names at the root of a drive: X:\Name or X:\Name\...
            string afterDrive = fullPath.Substring(3);
            int cut = afterDrive.IndexOf('\\');
            string top = cut < 0 ? afterDrive : afterDrive.Substring(0, cut);
            if (cut < 0 && Matches(top, RootFiles))
            {
                return true;
            }
            if (Matches(top, AnyDriveNames))
            {
                return true;
            }
            if (Matches(top, SystemDriveNames)
                && SameDrive(fullPath, Environment.GetFolderPath(Environment.SpecialFolder.System)))
            {
                return true;
            }

            // Registry hives of any profile.
            string name = Path.GetFileName(fullPath);
            if (name.StartsWith("NTUSER.DAT", StringComparison.OrdinalIgnoreCase))
            {
                string parent = Path.GetDirectoryName(fullPath);
                string profiles = parent == null ? null : Path.GetDirectoryName(parent);
                string own = Path.GetDirectoryName(ProfileFolder);
                if (profiles != null && own != null && string.Equals(profiles, own, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if a protected folder lies inside <paramref name="fullPath"/> (it is an ancestor of one).</summary>
        public static bool ContainsProtected(string fullPath)
        {
            foreach (string folder in ProtectedFolders())
            {
                if (IsInside(folder, fullPath))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- helpers ----

        private static string[] ProtectedFolders()
        {
            lock (sync)
            {
                if (protectedFolders != null)
                {
                    return protectedFolders;
                }
                List<string> list = new List<string>();
                Add(list, Environment.GetEnvironmentVariable("SystemRoot"));
                Add(list, Environment.GetFolderPath(Environment.SpecialFolder.System));
                Add(list, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
                Add(list, Environment.GetEnvironmentVariable("ProgramFiles"));
                Add(list, Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
                Add(list, Environment.GetEnvironmentVariable("ProgramW6432"));
                // ProgramData (on XP "All Users\Application Data").
                Add(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

                // AppData of this user and every other profile next to it (Application Data and Local Settings on XP).
                string profile = ProfileFolder;
                string[] appDataNames = new string[] { "AppData", "Application Data", "Local Settings" };
                string profiles = string.IsNullOrEmpty(profile) ? null : Path.GetDirectoryName(profile);
                if (profiles != null && Directory.Exists(profiles))
                {
                    try
                    {
                        foreach (string dir in Directory.GetDirectories(profiles))
                        {
                            AddAppData(list, dir, appDataNames);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
                if (!string.IsNullOrEmpty(profile))
                {
                    AddAppData(list, profile, appDataNames);
                }
                Add(list, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                Add(list, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

                // A protected folder that holds the profile itself (only possible when the profile is relocated) would
                // hide everything that is shared, so it does not count.
                if (!string.IsNullOrEmpty(profile))
                {
                    list.RemoveAll(delegate(string folder) { return IsInside(profile, folder); });
                }
                protectedFolders = list.ToArray();
                return protectedFolders;
            }
        }

        private static void AddAppData(List<string> list, string profileDir, string[] names)
        {
            foreach (string name in names)
            {
                Add(list, Path.Combine(profileDir, name));
            }
        }

        /// <summary>Adds a folder (full, rooted, not a drive root) once.</summary>
        private static void Add(List<string> list, string folder)
        {
            string path = TrimEnd(folder);
            if (path.Length <= 3 || !Path.IsPathRooted(path))
            {
                return;
            }
            foreach (string known in list)
            {
                if (string.Equals(known, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            list.Add(path);
        }

        private static bool Matches(string name, string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SameDrive(string a, string b)
        {
            return !string.IsNullOrEmpty(b) && b.Length >= 2 && a.Length >= 2
                && char.ToUpperInvariant(a[0]) == char.ToUpperInvariant(b[0]) && a[1] == ':' && b[1] == ':';
        }

        private static string TrimEnd(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            return path.Length > 3 ? path.TrimEnd('\\') : path;
        }

        /// <summary>True if <paramref name="path"/> is <paramref name="folder"/> or lies inside it.</summary>
        public static bool IsInside(string path, string folder)
        {
            if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (path.Length == folder.Length || folder.EndsWith("\\", StringComparison.Ordinal))
            {
                return true;
            }
            return path[folder.Length] == '\\';
        }
    }
}
