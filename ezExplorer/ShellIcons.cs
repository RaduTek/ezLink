using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// Icons and type names for remote files, taken from the local shell by extension only (the remote files do
    /// not exist here, so SHGFI_USEFILEATTRIBUTES is always set). The system image lists are shared by the tree
    /// and the list view. Use from the UI thread.
    /// </summary>
    internal static class ShellIcons
    {
        public struct Info
        {
            public int Index;
            public string TypeName;
        }

        private static readonly uint InfoSize = (uint)Marshal.SizeOf(typeof(ShellNative.SHFILEINFO));
        private static readonly Dictionary<string, Info> byExtension =
            new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);

        private static bool initialized;
        private static IntPtr smallList;
        private static IntPtr largeList;
        private static Info folder;
        private static int folderOpen;
        private static int fixedDrive = -1;
        private static int removableDrive = -1;
        private static int computer = -1;

        /// <summary>The system small image list (shared, never destroyed).</summary>
        public static IntPtr SmallList
        {
            get { Init(); return smallList; }
        }

        public static IntPtr LargeList
        {
            get { Init(); return largeList; }
        }

        public static int FolderIndex
        {
            get { Init(); return folder.Index; }
        }

        public static int FolderOpenIndex
        {
            get { Init(); return folderOpen; }
        }

        public static int ComputerIndex
        {
            get { Init(); return computer; }
        }

        public static Info ForFolder()
        {
            Init();
            return folder;
        }

        public static Info ForFile(string name)
        {
            Init();
            string extension = Path.GetExtension(name);
            Info info;
            if (byExtension.TryGetValue(extension, out info))
            {
                return info;
            }

            ShellNative.SHFILEINFO fi = new ShellNative.SHFILEINFO();
            // Pass a made-up name that only carries the extension, with normal file attributes.
            ShellNative.SHGetFileInfo("file" + extension, ShellNative.FILE_ATTRIBUTE_NORMAL, ref fi, InfoSize,
                ShellNative.SHGFI_USEFILEATTRIBUTES | ShellNative.SHGFI_SYSICONINDEX | ShellNative.SHGFI_TYPENAME
                | ShellNative.SHGFI_SMALLICON);
            info.Index = fi.iIcon;
            info.TypeName = string.IsNullOrEmpty(fi.szTypeName)
                ? (extension.Length > 1
                    ? string.Format(Strings.Get("TypeFile"), extension.Substring(1).ToUpperInvariant())
                    : Strings.Get("TypeFileNoExtension"))
                : fi.szTypeName;
            byExtension[extension] = info;
            return info;
        }

        /// <param name="driveType">A System.IO.DriveType value as sent by the file server.</param>
        public static int ForDrive(byte driveType)
        {
            Init();
            if (driveType == (byte)DriveType.Removable)
            {
                return removableDrive >= 0 ? removableDrive : folder.Index;
            }
            return fixedDrive >= 0 ? fixedDrive : folder.Index;
        }

        /// <summary>Gives a list view or tree view the "explorer" look (Vista and later; harmless elsewhere).</summary>
        public static void ApplyExplorerTheme(IntPtr handle)
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                return;
            }
            try
            {
                ShellNative.SetWindowTheme(handle, "explorer", null);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private static void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            ShellNative.SHFILEINFO fi = new ShellNative.SHFILEINFO();
            uint attributes = ShellNative.SHGFI_USEFILEATTRIBUTES | ShellNative.SHGFI_SYSICONINDEX;

            smallList = ShellNative.SHGetFileInfo(".txt", ShellNative.FILE_ATTRIBUTE_NORMAL, ref fi, InfoSize,
                attributes | ShellNative.SHGFI_SMALLICON);
            largeList = ShellNative.SHGetFileInfo(".txt", ShellNative.FILE_ATTRIBUTE_NORMAL, ref fi, InfoSize,
                attributes | ShellNative.SHGFI_LARGEICON);

            fi = new ShellNative.SHFILEINFO();
            ShellNative.SHGetFileInfo("folder", ShellNative.FILE_ATTRIBUTE_DIRECTORY, ref fi, InfoSize,
                attributes | ShellNative.SHGFI_TYPENAME | ShellNative.SHGFI_SMALLICON);
            folder.Index = fi.iIcon;
            folder.TypeName = string.IsNullOrEmpty(fi.szTypeName) ? Strings.Get("TypeFolder") : fi.szTypeName;

            fi = new ShellNative.SHFILEINFO();
            ShellNative.SHGetFileInfo("folder", ShellNative.FILE_ATTRIBUTE_DIRECTORY, ref fi, InfoSize,
                attributes | ShellNative.SHGFI_SMALLICON | ShellNative.SHGFI_OPENICON);
            folderOpen = fi.iIcon;

            fixedDrive = StockIcon(ShellNative.SIID_DRIVEFIXED);
            removableDrive = StockIcon(ShellNative.SIID_DRIVEREMOVABLE);
            computer = StockIcon(ShellNative.SIID_DESKTOPPC);
            if (fixedDrive < 0)
            {
                // Windows XP has no stock icons: ask for the icon of the local system drive instead.
                fi = new ShellNative.SHFILEINFO();
                string root = Path.GetPathRoot(Environment.SystemDirectory);
                ShellNative.SHGetFileInfo(root, 0, ref fi, InfoSize,
                    ShellNative.SHGFI_SYSICONINDEX | ShellNative.SHGFI_SMALLICON);
                fixedDrive = fi.iIcon;
            }
            if (computer < 0)
            {
                computer = fixedDrive;
            }
        }

        /// <summary>The system image index of a Vista+ stock icon, or -1 where the API does not exist.</summary>
        private static int StockIcon(uint id)
        {
            try
            {
                ShellNative.SHSTOCKICONINFO info = new ShellNative.SHSTOCKICONINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(ShellNative.SHSTOCKICONINFO));
                int hr = ShellNative.SHGetStockIconInfo(id, ShellNative.SHGSI_SYSICONINDEX, ref info);
                return hr == 0 ? info.iSysImageIndex : -1;
            }
            catch (EntryPointNotFoundException)
            {
                return -1;
            }
        }
    }
}
