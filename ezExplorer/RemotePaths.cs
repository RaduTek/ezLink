using System;
using System.Collections.Generic;
using EzLink.Protocol;

namespace ezExplorer
{
    /// <summary>
    /// Path helpers for the peer's file system. The empty string is the "computer" level that lists the drives;
    /// everything else is a full Windows path ("C:\", "C:\Users\Ana"). Drive roots keep their trailing backslash.
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
            if (IsDriveRoot(path))
            {
                return Root;
            }
            int cut = path.LastIndexOf('\\');
            if (cut <= 2)
            {
                return path.Substring(0, 3);
            }
            return path.Substring(0, cut);
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

        /// <summary>Protected operating-system files (hidden and system together) are not listed, as in Explorer.</summary>
        public static bool IsProtected(uint attributes)
        {
            uint both = ShellNative.FILE_ATTRIBUTE_HIDDEN | ShellNative.FILE_ATTRIBUTE_SYSTEM;
            return (attributes & both) == both;
        }

        public static bool Allows(Permissions granted, Permissions needed)
        {
            return (granted & needed) == needed;
        }
    }
}
