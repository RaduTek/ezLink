using System;
using System.IO;

namespace EzLink
{
    /// <summary>A path from the peer was refused; the message is safe to send back.</summary>
    public sealed class PathRejectedException : Exception
    {
        public PathRejectedException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Turns a path received from the peer into a full local path, or refuses it. Peers send full Windows
    /// paths ("C:\Users\Ana"); everything else is refused: relative paths (there is no working folder),
    /// drive-relative ("C:foo"), UNC and device paths ("\\server\share", "\\?\C:\", "\\.\COM1"), alternate data
    /// streams, device names such as NUL, and drives other than fixed and removable ones.
    /// </summary>
    public static class RemotePath
    {
        private static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>Returns the normalized full path, or throws <see cref="PathRejectedException"/>.</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('\0') >= 0)
            {
                throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed"));
            }
            // Must start with a drive letter and a separator. This rules out relative, drive-relative, UNC and
            // "\\?\" paths before GetFullPath gets a chance to interpret them.
            if (path.Length < 3 || !IsDriveLetter(path[0]) || path[1] != ':' || (path[2] != '\\' && path[2] != '/'))
            {
                throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed"));
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException
                    || ex is System.Security.SecurityException)
                {
                    throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed"));
                }
                throw;
            }

            // After normalization it must still be a plain drive path with no ":" other than the drive's.
            if (full.Length < 3 || !IsDriveLetter(full[0]) || full[1] != ':' || full[2] != '\\'
                || full.IndexOf(':', 2) >= 0 || full.StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed"));
            }

            foreach (string part in full.Substring(3).Split('\\'))
            {
                if (IsReservedName(part))
                {
                    throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed"));
                }
            }

            DriveType type = new DriveInfo(full.Substring(0, 3)).DriveType;
            if (type != DriveType.Fixed && type != DriveType.Removable)
            {
                throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed"));
            }
            return full;
        }

        /// <summary>True for the root of a drive ("C:\").</summary>
        public static bool IsDriveRoot(string fullPath)
        {
            return fullPath.Length == 3;
        }

        private static bool IsDriveLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        /// <summary>"nul", "NUL.txt" and "com1 " all name devices on Windows.</summary>
        private static bool IsReservedName(string part)
        {
            int dot = part.IndexOf('.');
            string stem = (dot >= 0 ? part.Substring(0, dot) : part).TrimEnd(' ');
            foreach (string reserved in ReservedNames)
            {
                if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
