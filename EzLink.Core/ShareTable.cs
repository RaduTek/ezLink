using System;
using System.Collections.Generic;
using System.IO;

namespace EzLink
{
    /// <summary>One root the peer can open: the profile folder, an extra folder, or a whole drive.</summary>
    public sealed class Share
    {
        /// <summary>The first part of every path below it: "Ana", "Photos", "C:".</summary>
        public string Name;
        /// <summary>The local folder; a drive keeps its trailing backslash ("C:\").</summary>
        public string FullPath;
        public bool ReadOnly;
        /// <summary>True for a whole drive.</summary>
        public bool IsDrive;
        /// <summary>The System.IO.DriveType of a drive; 0 for a folder.</summary>
        public byte DriveType;
    }

    /// <summary>
    /// The shares offered right now, built from <see cref="SharingSettings"/>: the profile folder (named by its
    /// folder name), the extra folders and drives from Options and the drives of the two "all drives" boxes. Every
    /// share is addressed by name, so names are unique (case-insensitive): when two clash the first one wins in
    /// the order profile, extra folders, drives. Peers send "Name" or "Name\sub\path"; a drive also keeps the
    /// "C:\..." form.
    /// </summary>
    public sealed class ShareTable
    {
        private readonly List<Share> shares = new List<Share>();

        private ShareTable()
        {
        }

        public List<Share> Shares
        {
            get { return shares; }
        }

        public static ShareTable Build(SharingSettings settings)
        {
            ShareTable table = new ShareTable();

            string profile = PathPolicy.ProfileFolder;
            if (settings.ShareUserProfile && profile.Length > 0)
            {
                Share share = new Share();
                share.Name = PathPolicy.ProfileName;
                share.FullPath = profile;
                share.ReadOnly = false; // the profile folder is always writable
                table.Add(share);
            }

            foreach (SharedFolder folder in settings.GetFolders())
            {
                string path = folder.Path;
                if (!Path.IsPathRooted(path))
                {
                    continue;
                }
                Share share = new Share();
                share.IsDrive = folder.IsDriveRoot;
                share.Name = folder.Name;
                share.FullPath = share.IsDrive ? char.ToUpperInvariant(path[0]) + @":\" : path.TrimEnd('\\');
                share.ReadOnly = folder.ReadOnly;
                if (share.IsDrive)
                {
                    share.DriveType = DriveTypeOf(share.FullPath);
                }
                if (share.Name.Length > 0)
                {
                    table.Add(share);
                }
            }

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                DriveType type = drive.DriveType;
                if (!PathPolicy.IsDriveTypeShared(type, settings))
                {
                    continue;
                }
                Share share = new Share();
                share.IsDrive = true;
                share.DriveType = (byte)type;
                share.FullPath = char.ToUpperInvariant(drive.Name[0]) + @":\";
                share.Name = share.FullPath.Substring(0, 2);
                share.ReadOnly = type == DriveType.Fixed ? settings.FixedDrivesReadOnly : settings.RemovableDrivesReadOnly;
                table.Add(share);
            }
            return table;
        }

        private static byte DriveTypeOf(string root)
        {
            try
            {
                return (byte)new DriveInfo(root).DriveType;
            }
            catch (ArgumentException)
            {
                return 0;
            }
        }

        /// <summary>Adds the share unless one with the same name (or the same folder) is already there.</summary>
        private void Add(Share share)
        {
            foreach (Share known in shares)
            {
                if (string.Equals(known.Name, share.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(known.FullPath, share.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Warn("Not sharing {0}: the name '{1}' is already used by {2}", share.FullPath, share.Name,
                            known.FullPath);
                    }
                    return;
                }
            }
            shares.Add(share);
        }

        public Share Find(string name)
        {
            foreach (Share share in shares)
            {
                if (string.Equals(share.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return share;
                }
            }
            return null;
        }

        /// <summary>
        /// Turns a path from the peer into a full local path inside one of the shares, or throws
        /// <see cref="PathRejectedException"/> (the message is safe to send back).
        /// </summary>
        public string Resolve(string wirePath, out Share share)
        {
            if (string.IsNullOrEmpty(wirePath) || wirePath.IndexOf('\0') >= 0)
            {
                throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed"));
            }

            // Drive form: "C:\Users". A bare "C:" or "C:foo" is refused by Normalize.
            if (wirePath.Length >= 2 && wirePath[1] == ':')
            {
                string fullDrivePath = RemotePath.Normalize(wirePath);
                share = Find(fullDrivePath.Substring(0, 2));
                if (share == null || !share.IsDrive)
                {
                    throw new PathRejectedException(Strings.Get("ErrorNotShared"));
                }
                return fullDrivePath;
            }

            // Named form: "Photos" or "Photos\2020\a.jpg".
            string text = wirePath.Replace('/', '\\');
            int cut = text.IndexOf('\\');
            string name = cut < 0 ? text : text.Substring(0, cut);
            string rest = cut < 0 ? string.Empty : text.Substring(cut + 1);
            share = Find(name);
            if (share == null)
            {
                throw new PathRejectedException(Strings.Get("ErrorNotShared"));
            }

            string combined = rest.Length == 0
                ? share.FullPath
                : (share.FullPath.EndsWith("\\", StringComparison.Ordinal) ? share.FullPath + rest : share.FullPath + "\\" + rest);
            string full = RemotePath.Normalize(combined);
            if (!PathPolicy.IsInside(full, share.FullPath))
            {
                throw new PathRejectedException(Strings.Get("ErrorPathNotAllowed")); // climbed out with ".."
            }
            return full;
        }

        /// <summary>True if the full path is the root folder of one of the shares.</summary>
        public bool IsShareRoot(string fullPath)
        {
            string trimmed = fullPath.Length > 3 ? fullPath.TrimEnd('\\') : fullPath;
            foreach (Share share in shares)
            {
                if (string.Equals(share.FullPath, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
