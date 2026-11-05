using System;
using System.Runtime.InteropServices;

namespace ezExplorer
{
    /// <summary>
    /// Shell and common-control declarations. Handles are IntPtr (never int) so they are correct in both 32- and
    /// 64-bit processes.
    /// </summary>
    internal static class ShellNative
    {
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_OPENICON = 0x2;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        public const uint SHGFI_TYPENAME = 0x400;
        public const uint SHGFI_SYSICONINDEX = 0x4000;

        public const uint FILE_ATTRIBUTE_READONLY = 0x1;
        public const uint FILE_ATTRIBUTE_HIDDEN = 0x2;
        public const uint FILE_ATTRIBUTE_SYSTEM = 0x4;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        public const uint SHGSI_SYSICONINDEX = 0x4000;
        public const uint SIID_FOLDER = 3;
        public const uint SIID_FOLDEROPEN = 4;
        public const uint SIID_DRIVEFIXED = 7;
        public const uint SIID_DRIVEREMOVABLE = 11;
        public const uint SIID_DESKTOPPC = 104;

        public const int LVM_FIRST = 0x1000;
        public const int LVM_SETIMAGELIST = LVM_FIRST + 3;
        public const int LVSIL_NORMAL = 0;
        public const int LVM_SETITEMSTATE = LVM_FIRST + 43;
        public const uint LVIS_SELECTED = 0x2;
        public const uint LVIS_DROPHILITED = 0x8;
        public const int LVSIL_SMALL = 1;

        public const int TV_FIRST = 0x1100;
        public const int TVM_SETIMAGELIST = TV_FIRST + 9;
        public const int TVM_SETITEMW = TV_FIRST + 63;
        public const int TVSIL_NORMAL = 0;
        public const int TVM_SELECTITEM = TV_FIRST + 11;
        public const int TVGN_DROPHILITE = 8;

        public const int WM_VSCROLL = 0x115;
        public const int SB_LINEUP = 0;
        public const int SB_LINEDOWN = 1;
        public const uint TVIF_IMAGE = 0x2;
        public const uint TVIF_SELECTEDIMAGE = 0x20;
        public const uint TVIF_HANDLE = 0x10;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHSTOCKICONINFO
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysImageIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TVITEM
        {
            public uint mask;
            public IntPtr hItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public int iSelectedImage;
            public int cChildren;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LVITEM
        {
            public uint mask;
            public int iItem;
            public int iSubItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public IntPtr lParam;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref SHFILEINFO info,
            uint infoSize, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHGetStockIconInfo(uint siid, uint flags, ref SHSTOCKICONINFO info);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref TVITEM item);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref LVITEM item);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string x, string y);
    }
}
