using System;
using System.Collections.Generic;
using EzLink;
using EzLink.Protocol;

namespace ezExplorer
{
    /// <summary>
    /// Path helpers for the peer's file system. The empty string is the "computer" level that lists the roots.
    /// A drive path is a full Windows path ("C:\", "C:\Users\Ana"; drive roots keep their trailing backslash) and a
    /// shared folder is addressed by its name ("Photos", "Photos\2020\a.jpg").
    /// </summary>
    internal static class RemotePaths
    {
        public const string Root = "";

        public static bool SamePath(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRoot(string path)
        {
            return path.Length == 0;
        }

        // The peer's roots (learned from its root list): "C:\" for a drive, the share's name for a shared folder.
        private static readonly object rootsLock = new object();
        private static readonly Dictionary<string, DriveEntry> roots =
            new Dictionary<string, DriveEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The path of a root as used in paths: "C:\" for a drive, otherwise the share's name.</summary>
        public static string RootPath(DriveEntry entry)
        {
            return entry.DriveType != 0 ? entry.Name + "\\" : entry.Name;
        }

        /// <summary>Remembers the peer's roots, for display names and read-only shares.</summary>
        public static void RememberRoots(List<DriveEntry> entries)
        {
            lock (rootsLock)
            {
                roots.Clear();
                foreach (DriveEntry entry in entries)
                {
                    roots[RootPath(entry)] = entry;
                }
            }
        }

        /// <summary>The root a path lies in: "C:\" for "C:\Users\Ana", "Photos" for "Photos\2020".</summary>
        public static string RootOf(string path)
        {
            if (path.Length == 0)
            {
                return string.Empty;
            }
            if (path.Length >= 2 && path[1] == ':')
            {
                return path.Substring(0, 2) + "\\";
            }
            int cut = path.IndexOf('\\');
            return cut < 0 ? path : path.Substring(0, cut);
        }

        /// <summary>True if the owner shares the root of this path read-only.</summary>
        public static bool IsReadOnly(string path)
        {
            lock (rootsLock)
            {
                DriveEntry entry;
                return path.Length > 0 && roots.TryGetValue(RootOf(path), out entry) && entry.ReadOnly;
            }
        }

        /// <summary>A root listed at the computer level: a drive ("C:\") or a shared folder ("Photos").</summary>
        public static bool IsTopLevel(string path)
        {
            return path.Length > 0 && (IsDriveRoot(path) || path.IndexOf('\\') < 0);
        }

        /// <summary>The name to show for a path segment: a root's label, a drive without its backslash, or the folder name.</summary>
        public static string DisplayName(string path)
        {
            if (IsTopLevel(path))
            {
                lock (rootsLock)
                {
                    DriveEntry entry;
                    if (roots.TryGetValue(path, out entry) && entry.DriveType == 0 && entry.Label.Length > 0)
                    {
                        return entry.Label;
                    }
                }
                return IsDriveRoot(path) ? path.TrimEnd('\\') : path;
            }
            return GetName(path);
        }

        public static bool IsDriveRoot(string path)
        {
            return path.Length == 3 && path[1] == ':' && path[2] == '\\';
        }

        public static string Combine(string folder, string name)
        {
            if (folder.Length == 0)
            {
                return name;
            }
            return folder.EndsWith("\\", StringComparison.Ordinal) ? folder + name : folder + "\\" + name;
        }

        /// <summary>The parent folder; "" (the computer level) for a drive root, null for the computer level.</summary>
        public static string GetParent(string path)
        {
            if (path.Length == 0)
            {
                return null;
            }
            if (IsTopLevel(path))
            {
                return Root;
            }
            int cut = path.LastIndexOf('\\');
            if (path[1] == ':' && cut <= 2)
            {
                return path.Substring(0, 3); // "C:\Users" -> "C:\"
            }
            return path.Substring(0, cut); // "Photos\2020" -> "Photos"
        }

        public static string GetName(string path)
        {
            if (IsDriveRoot(path))
            {
                return path;
            }
            return path.Substring(path.LastIndexOf('\\') + 1);
        }

        /// <summary>True if <paramref name="path"/> is <paramref name="folder"/> or lies inside it.</summary>
        public static bool IsInside(string path, string folder)
        {
            if (folder.Length == 0)
            {
                return true;
            }
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

        /// <summary>
        /// The chain of folders from the first below the computer level down to the path itself:
        /// "C:\Users\Ana" gives "C:\", "C:\Users", "C:\Users\Ana". Empty for the computer level.
        /// </summary>
        public static List<string> Chain(string path)
        {
            List<string> chain = new List<string>();
            while (path != null && path.Length > 0)
            {
                chain.Insert(0, path);
                path = GetParent(path);
            }
            return chain;
        }

        /// <summary>
        /// Cleans up text typed into the path box: slashes, a bare drive letter, a trailing backslash. Whether the
        /// result exists is for the peer to say.
        /// </summary>
        public static string FromUserText(string text)
        {
            string path = text.Trim().Trim('"').Replace('/', '\\');
            if (path.Length == 2 && path[1] == ':')
            {
                path += "\\";
            }
            while (path.Length > 3 && path.EndsWith("\\", StringComparison.Ordinal))
            {
                path = path.Substring(0, path.Length - 1);
            }
            if (path.Length >= 3 && path[1] == ':' && path[0] >= 'a' && path[0] <= 'z')
            {
                path = char.ToUpperInvariant(path[0]) + path.Substring(1);
            }
            return path;
        }

        /// <summary>A file name the peer would accept: not empty, no path characters.</summary>
        public static bool IsValidName(string name)
        {
            if (name == null || name.Trim().Length == 0 || name == "." || name == "..")
            {
                return false;
            }
            return name.IndexOfAny(new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\0' }) < 0;
        }

        public static bool IsFolder(uint attributes)
        {
            return (attributes & ShellNative.FILE_ATTRIBUTE_DIRECTORY) != 0;
        }

        /// <summary>Hidden and system items are listed only when the matching option is on.</summary>
        public static bool IsVisible(uint attributes, bool showHidden, bool showSystem)
        {
            if (!showHidden && (attributes & ShellNative.FILE_ATTRIBUTE_HIDDEN) != 0)
            {
                return false;
            }
            if (!showSystem && (attributes & ShellNative.FILE_ATTRIBUTE_SYSTEM) != 0)
            {
                return false;
            }
            return true;
        }

        public static bool Allows(Permissions granted, Permissions needed)
        {
            return (granted & needed) == needed;
        }
    }
}
