using System;
using System.Collections.Generic;
using System.IO;

namespace EzLink
{
    /// <summary>
    /// An extra folder or whole drive the user chose to share in Options. Its name on the link is derived from
    /// the path: the folder's name, or the drive letter ("D:") for a drive root.
    /// </summary>
    public sealed class SharedFolder
    {
        public string Path = string.Empty;
        public bool ReadOnly;

        public SharedFolder()
        {
        }

        public SharedFolder(string path, bool readOnly)
        {
            Path = path;
            ReadOnly = readOnly;
        }

        public SharedFolder Clone()
        {
            return new SharedFolder(Path, ReadOnly);
        }

        /// <summary>The name the other computer sees: "Photos" for D:\Photos, "D:" for D:\.</summary>
        public string Name
        {
            get { return NameOf(Path); }
        }

        public bool IsDriveRoot
        {
            get { return IsDriveRootPath(Path); }
        }

        public static bool IsDriveRootPath(string path)
        {
            return path != null && path.Length == 3 && path[1] == ':' && path[2] == '\\';
        }

        /// <summary>The share name for a full path: the last folder name, or "X:" for a drive root.</summary>
        public static string NameOf(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            if (IsDriveRootPath(path))
            {
                return char.ToUpperInvariant(path[0]) + ":";
            }
            string trimmed = path.TrimEnd('\\');
            return System.IO.Path.GetFileName(trimmed);
        }

        // ---- registry text: "R|path" (read-only) / "W|path" (writable) ----

        public string Format()
        {
            return (ReadOnly ? "R|" : "W|") + Path;
        }

        /// <summary>Returns null for text that is not an entry.</summary>
        public static SharedFolder Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 3 || text[1] != '|' || (text[0] != 'R' && text[0] != 'W'))
            {
                return null;
            }
            string path = text.Substring(2);
            if (!System.IO.Path.IsPathRooted(path))
            {
                return null;
            }
            return new SharedFolder(path, text[0] == 'R');
        }

        public static List<SharedFolder> ParseAll(string[] lines)
        {
            List<SharedFolder> list = new List<SharedFolder>();
            if (lines != null)
            {
                foreach (string line in lines)
                {
                    SharedFolder folder = Parse(line);
                    if (folder != null)
                    {
                        list.Add(folder);
                    }
                }
            }
            return list;
        }

        public static string[] FormatAll(List<SharedFolder> list)
        {
            string[] lines = new string[list.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = list[i].Format();
            }
            return lines;
        }
    }
}
