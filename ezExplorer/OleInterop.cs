using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ezExplorer
{
    /// <summary>
    /// OLE drag-and-drop, clipboard and shell-helper declarations. Everything that holds a pointer or a handle uses
    /// IntPtr (never int), so the signatures are right in both 32- and 64-bit processes.
    /// </summary>
    internal static class Ole
    {
        // HRESULTs
        public const int S_OK = 0;
        public const int S_FALSE = 1;
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_ABORT = unchecked((int)0x80004004);
        public const int HR_CANCELLED = unchecked((int)0x800704C7); // HRESULT_FROM_WIN32(ERROR_CANCELLED)
        public const int E_FAIL = unchecked((int)0x80004005);
        public const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
        public const int E_INVALIDARG = unchecked((int)0x80070057);
        public const int ERROR_READ_FAULT = unchecked((int)0x8007001E);
        public const int DV_E_FORMATETC = unchecked((int)0x80040064);
        public const int DV_E_TYMED = unchecked((int)0x80040069);
        public const int DV_E_DVASPECT = unchecked((int)0x8004006B);
        public const int OLE_E_ADVISENOTSUPPORTED = unchecked((int)0x80040003);
        public const int DATA_S_SAMEFORMATETC = 0x00040130;
        public const int STG_E_INVALIDFUNCTION = unchecked((int)0x80030001);
        public const int STG_E_ACCESSDENIED = unchecked((int)0x80030005);
        public const int DRAGDROP_S_DROP = 0x00040100;
        public const int DRAGDROP_S_CANCEL = 0x00040101;
        public const int DRAGDROP_S_USEDEFAULTCURSORS = 0x00040102;

        // Clipboard format names and drop effects
        public const string CFSTR_FILEDESCRIPTORW = "FileGroupDescriptorW";
        public const string CFSTR_FILECONTENTS = "FileContents";
        public const string CFSTR_PREFERREDDROPEFFECT = "Preferred DropEffect";
        public const int DROPEFFECT_NONE = 0;
        public const int DROPEFFECT_COPY = 1;

        // FILEDESCRIPTOR flags
        public const uint FD_ATTRIBUTES = 0x4;
        public const uint FD_WRITESTIME = 0x20;
        public const uint FD_FILESIZE = 0x40;
        // FD_PROGRESSUI (0x4000) is deliberately never set: the transfer dialog is the only progress window.

        public const int MK_LBUTTON = 0x1;
        public const int MK_RBUTTON = 0x2;

        public const uint GMEM_MOVEABLE = 0x2;
        public const uint GMEM_ZEROINIT = 0x40;

        public const int STATFLAG_NONAME = 1;
        public const int STGTY_STREAM = 2;

        public const int DATADIR_GET = 1;

        /// <summary>The size of a FILEDESCRIPTORW (flags, clsid, size, point, attributes, three times, size, name).</summary>
        public const int FileDescriptorSize = 592;
        public const int MaxPath = 260;

        public static readonly Guid CLSID_DragDropHelper = new Guid("4657278A-411B-11d2-839A-00C04FD918D0");

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;

            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHDRAGIMAGE
        {
            public SIZE sizeDragImage;
            public POINT ptOffset;
            public IntPtr hbmpDragImage;
            public int crColorKey;
        }

        [DllImport("ole32.dll")]
        public static extern int OleInitialize(IntPtr reserved);

        [DllImport("ole32.dll")]
        public static extern int DoDragDrop([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
            [MarshalAs(UnmanagedType.Interface)] IDropSource dropSource, int allowedEffects, out int effect);

        [DllImport("ole32.dll")]
        public static extern int OleSetClipboard(
            [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject);

        [DllImport("ole32.dll")]
        public static extern int OleIsCurrentClipboard(
            [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject);

        [DllImport("ole32.dll")]
        public static extern void ReleaseStgMedium(ref STGMEDIUM medium);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GlobalLock(IntPtr handle);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr handle);

        [DllImport("kernel32.dll")]
        public static extern UIntPtr GlobalSize(IntPtr handle);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GlobalFree(IntPtr handle);

        [DllImport("ole32.dll")]
        private static extern int CreateStreamOnHGlobal(IntPtr hGlobal, [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease,
            [MarshalAs(UnmanagedType.Interface)] out IStream stream);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr handle);

        [DllImport("comctl32.dll")]
        public static extern bool ImageList_Draw(IntPtr imageList, int index, IntPtr hdc, int x, int y, uint style);

        public const uint ILD_TRANSPARENT = 0x1;

        /// <summary>Copies bytes into a new movable global block, the memory kind a TYMED_HGLOBAL medium owns.</summary>
        public static IntPtr AllocGlobal(byte[] data)
        {
            IntPtr handle = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, new UIntPtr((uint)Math.Max(1, data.Length)));
            if (handle == IntPtr.Zero)
            {
                throw new COMException("Out of memory", E_OUTOFMEMORY);
            }
            IntPtr target = GlobalLock(handle);
            if (target == IntPtr.Zero)
            {
                GlobalFree(handle);
                throw new COMException("Out of memory", E_OUTOFMEMORY);
            }
            Marshal.Copy(data, 0, target, data.Length);
            GlobalUnlock(handle);
            return handle;
        }

        /// <summary>A new memory stream holding the bytes, as an IStream pointer a medium can own (null on failure).</summary>
        public static IntPtr NewStreamOn(byte[] data)
        {
            IStream stream;
            Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(IntPtr.Zero, true, out stream));
            if (data.Length > 0)
            {
                stream.Write(data, data.Length, IntPtr.Zero);
            }
            stream.Seek(0, 0, IntPtr.Zero);
            return Marshal.GetComInterfaceForObject(stream, typeof(IStream));
        }

        /// <summary>Reads a whole IStream (given as a COM pointer) from its start into a byte array.</summary>
        public static byte[] ReadStream(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return null;
            }
            IStream stream = (IStream)Marshal.GetObjectForIUnknown(pointer);
            System.Runtime.InteropServices.ComTypes.STATSTG stat;
            stream.Stat(out stat, STATFLAG_NONAME);
            stream.Seek(0, 0, IntPtr.Zero);
            byte[] data = new byte[stat.cbSize];
            IntPtr read = Marshal.AllocHGlobal(4);
            try
            {
                int total = 0;
                while (total < data.Length)
                {
                    byte[] chunk = new byte[Math.Min(65536, data.Length - total)];
                    stream.Read(chunk, chunk.Length, read);
                    int n = Marshal.ReadInt32(read);
                    if (n <= 0)
                    {
                        break;
                    }
                    Buffer.BlockCopy(chunk, 0, data, total, n);
                    total += n;
                }
                return data;
            }
            finally
            {
                Marshal.FreeHGlobal(read);
            }
        }

        /// <summary>Reads a global block into a byte array (null if it cannot be locked).</summary>
        public static byte[] ReadGlobal(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return null;
            }
            long size = (long)GlobalSize(handle);
            IntPtr source = GlobalLock(handle);
            if (source == IntPtr.Zero)
            {
                return null;
            }
            try
            {
                byte[] data = new byte[size];
                Marshal.Copy(source, data, 0, data.Length);
                return data;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
    }

    /// <summary>OLE's IDropSource. The data object is the drag source, so this only decides when the drag ends.</summary>
    [ComImport, Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDropSource
    {
        [PreserveSig]
        int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escapePressed, int keyState);

        [PreserveSig]
        int GiveFeedback(int effect);
    }

    /// <summary>
    /// IAsyncOperation (IDataObjectAsyncCapability): lets the drop target pull the data on its own thread, so
    /// neither Explorer nor this app freezes while a big copy runs.
    /// </summary>
    [ComImport, Guid("3D8B0590-F691-11d2-8EA9-006097DF5BD4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAsyncOperation
    {
        void SetAsyncMode([MarshalAs(UnmanagedType.Bool)] bool doOpAsync);

        void GetAsyncMode([MarshalAs(UnmanagedType.Bool)] out bool isOpAsync);

        void StartOperation(IBindCtx reserved);

        void InOperation([MarshalAs(UnmanagedType.Bool)] out bool inAsyncOp);

        void EndOperation(int result, IBindCtx reserved, int effects);
    }

    /// <summary>The shell's drag image provider for the drag source.</summary>
    [ComImport, Guid("DE5BF786-477A-11D2-839D-00C04FD918D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDragSourceHelper
    {
        [PreserveSig]
        int InitializeFromBitmap(ref Ole.SHDRAGIMAGE dragImage,
            [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject);

        [PreserveSig]
        int InitializeFromWindow(IntPtr window, ref Ole.POINT point,
            [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject);
    }

    /// <summary>The shell's drag image provider for a drop target: shows the source's drag image over our window.</summary>
    [ComImport, Guid("4657278B-411B-11D2-839A-00C04FD918D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDropTargetHelper
    {
        [PreserveSig]
        int DragEnter(IntPtr targetWindow,
            [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
            ref Ole.POINT point, int effect);

        [PreserveSig]
        int DragLeave();

        [PreserveSig]
        int DragOver(ref Ole.POINT point, int effect);

        [PreserveSig]
        int Drop([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
            ref Ole.POINT point, int effect);

        [PreserveSig]
        int Show([MarshalAs(UnmanagedType.Bool)] bool show);
    }
}
