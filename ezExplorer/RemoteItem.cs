using System;
using System.Collections.Generic;
using System.IO;
using EzLink;
using EzLink.Protocol;

namespace ezExplorer
{
    /// <summary>One row of the file list: a drive, a folder or a file on the peer.</summary>
    internal sealed class RemoteItem
    {
        /// <summary>The name shown in the list (for a drive: "Local Disk (C:)").</summary>
        public string DisplayName;
        /// <summary>The real file or folder name (for a drive: "C:\").</summary>
        public string Name;
        /// <summary>The full path on the peer.</summary>
        public string Path;
        public bool IsFolder;
        public bool IsDrive;
        public uint Attributes;
        public long Size;
        public bool HasDate;
        public DateTime Modified;       // local time
        public string TypeName;
        public int IconIndex;

        public static RemoteItem FromDrive(DriveEntry drive)
        {
            RemoteItem item = new RemoteItem();
            item.Name = drive.Letter + ":\\";
            item.Path = item.Name;
            item.IsFolder = true;
            item.IsDrive = true;
            item.Attributes = ShellNative.FILE_ATTRIBUTE_DIRECTORY;
            item.Size = drive.TotalBytes;
            bool removable = drive.DriveType == (byte)DriveType.Removable;
            item.TypeName = Strings.Get(removable ? "DriveRemovable" : "DriveFixed");
            string label = drive.Label.Length > 0 ? drive.Label : item.TypeName;
            item.DisplayName = string.Format(Strings.Get("DriveName"), label, drive.Letter);
            item.IconIndex = ShellIcons.ForDrive(drive.DriveType);
            return item;
        }

        public static RemoteItem FromEntry(string folder, FileEntry entry)
        {
            RemoteItem item = new RemoteItem();
            item.Name = entry.Name;
            item.DisplayName = entry.Name;
            item.Path = RemotePaths.Combine(folder, entry.Name);
            item.Attributes = entry.Attributes;
            item.IsFolder = RemotePaths.IsFolder(entry.Attributes);
            item.Size = item.IsFolder ? 0 : entry.Size;
            item.HasDate = true;
            item.Modified = entry.Modified.Kind == DateTimeKind.Utc ? entry.Modified.ToLocalTime() : entry.Modified;
            ShellIcons.Info info = item.IsFolder ? ShellIcons.ForFolder() : ShellIcons.ForFile(entry.Name);
            item.TypeName = info.TypeName;
            item.IconIndex = info.Index;
            return item;
        }

        public static List<RemoteItem> FromDrives(List<DriveEntry> drives)
        {
            List<RemoteItem> items = new List<RemoteItem>(drives.Count);
            foreach (DriveEntry drive in drives)
            {
                items.Add(FromDrive(drive));
            }
            return items;
        }

        public static List<RemoteItem> FromEntries(string folder, List<FileEntry> entries)
        {
            List<RemoteItem> items = new List<RemoteItem>(entries.Count);
            foreach (FileEntry entry in entries)
            {
                if (!RemotePaths.IsProtected(entry.Attributes))
                {
                    items.Add(FromEntry(folder, entry));
                }
            }
            return items;
        }
    }
}
