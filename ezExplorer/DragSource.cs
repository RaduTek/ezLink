using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>Starts drags and clipboard copies of remote files, with the shell's own drag image.</summary>
    internal static class DragSource
    {
        private sealed class DropSource : IDropSource
        {
            private readonly int buttonMask;

            public DropSource(MouseButtons button)
            {
                buttonMask = button == MouseButtons.Right ? Ole.MK_RBUTTON : Ole.MK_LBUTTON;
            }

            public int QueryContinueDrag(bool escapePressed, int keyState)
            {
                if (escapePressed)
                {
                    return Ole.DRAGDROP_S_CANCEL;
                }
                if ((keyState & buttonMask) == 0)
                {
                    return Ole.DRAGDROP_S_DROP;
                }
                return Ole.S_OK;
            }

            public int GiveFeedback(int effect)
            {
                return Ole.DRAGDROP_S_USEDEFAULTCURSORS;
            }
        }

        /// <summary>
        /// Runs the drag with <c>DoDragDrop</c> from ole32 (the WinForms method cannot carry a virtual-file data
        /// object). Only a copy is allowed. Returns the effect the target reported.
        /// </summary>
        public static int Drag(VirtualFileDataObject data, MouseButtons button, List<RemoteItem> items)
        {
            Ole.OleInitialize(IntPtr.Zero); // a no-op if WinForms already did it

            IDragSourceHelper helper = null;
            try
            {
                helper = CreateHelper() as IDragSourceHelper;
                if (helper != null)
                {
                    Ole.SHDRAGIMAGE image;
                    if (DragImage.TryCreate(items, out image))
                    {
                        if (helper.InitializeFromBitmap(ref image, data) != Ole.S_OK)
                        {
                            Ole.DeleteObject(image.hbmpDragImage); // the helper only takes the bitmap if it succeeds
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("No drag image: {0}", ex.Message);
            }

            int effect;
            int result = Ole.DoDragDrop(data, new DropSource(button), Ole.DROPEFFECT_COPY, out effect);
            if (helper != null)
            {
                Marshal.ReleaseComObject(helper);
            }
            Log.Info("Drag ended: result 0x{0:X8}, effect {1}", result, effect);
            return result == Ole.DRAGDROP_S_DROP ? effect : Ole.DROPEFFECT_NONE;
        }

        /// <summary>
        /// Puts the data object on the clipboard. It stays virtual: <c>OleFlushClipboard</c> is never called, so
        /// nothing is read until a paste asks for it. Returns false if the clipboard could not be opened.
        /// </summary>
        public static bool Copy(VirtualFileDataObject data)
        {
            Ole.OleInitialize(IntPtr.Zero); // a no-op if WinForms already did it
            int result = 0;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                result = Ole.OleSetClipboard(data);
                if (result == Ole.S_OK)
                {
                    return true;
                }
                Thread.Sleep(50); // another program has the clipboard open for a moment
            }
            Log.Warn("OleSetClipboard failed: 0x{0:X8}", result);
            return false;
        }

        /// <summary>Takes our data off the clipboard if it is still there, so a later paste fails cleanly.</summary>
        public static void ClearClipboardIf(VirtualFileDataObject data)
        {
            if (data != null && Ole.OleIsCurrentClipboard(data) == Ole.S_OK)
            {
                Ole.OleSetClipboard(null);
            }
        }

        private static object CreateHelper()
        {
            Type type = Type.GetTypeFromCLSID(Ole.CLSID_DragDropHelper);
            return Activator.CreateInstance(type);
        }

        /// <summary>The shell's helper for a drop target, or null if it cannot be created.</summary>
        internal static IDropTargetHelper CreateTargetHelper()
        {
            try
            {
                return CreateHelper() as IDropTargetHelper;
            }
            catch (Exception ex)
            {
                Log.Warn("No drop target helper: {0}", ex.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// Makes the drag image: the first item's icon and name (and a count) on the selection colour. It is drawn
    /// with a colour key rather than alpha, so it also works on Windows XP.
    /// </summary>
    internal static class DragImage
    {
        public static bool TryCreate(List<RemoteItem> items, out Ole.SHDRAGIMAGE image)
        {
            image = new Ole.SHDRAGIMAGE();
            if (items == null || items.Count == 0)
            {
                return false;
            }

            string text = items.Count == 1
                ? items[0].DisplayName
                : string.Format(Strings.Get("DragManyItems"), items.Count);
            Font font = SystemFonts.MessageBoxFont;
            Color key = Color.Magenta;

            int iconSize = 16;
            Size textSize;
            using (Bitmap probe = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
            {
                SizeF measured = g.MeasureString(text, font);
                textSize = new Size((int)Math.Ceiling(measured.Width), (int)Math.Ceiling(measured.Height));
            }
            int width = Math.Min(260, 8 + iconSize + 6 + textSize.Width + 8);
            int height = Math.Max(iconSize, textSize.Height) + 8;

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            try
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(key);
                    using (Brush back = new SolidBrush(SystemColors.Highlight))
                    {
                        g.FillRectangle(back, 0, 0, width, height);
                    }
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        Ole.ImageList_Draw(ShellIcons.SmallList, items[0].IconIndex, hdc, 6, (height - iconSize) / 2,
                            Ole.ILD_TRANSPARENT);
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                    using (Brush fore = new SolidBrush(SystemColors.HighlightText))
                    {
                        StringFormat format = new StringFormat(StringFormatFlags.NoWrap);
                        format.Trimming = StringTrimming.EllipsisCharacter;
                        format.LineAlignment = StringAlignment.Center;
                        g.DrawString(text, font, fore,
                            new RectangleF(6 + iconSize + 6, 0, width - (6 + iconSize + 6) - 6, height), format);
                    }
                }

                image.sizeDragImage.cx = width;
                image.sizeDragImage.cy = height;
                image.ptOffset.x = 14;
                image.ptOffset.y = height / 2;
                image.hbmpDragImage = bitmap.GetHbitmap(key);
                image.crColorKey = ColorTranslator.ToWin32(key);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Cannot draw the drag image: {0}", ex.Message);
                return false;
            }
            finally
            {
                bitmap.Dispose();
            }
        }
    }

    /// <summary>
    /// Lets a pane show the drag image of files dragged in from Explorer (WinForms drop targets do not, on their
    /// own). One instance per control; every call is best-effort.
    /// </summary>
    internal sealed class DropTargetHelper : IDisposable
    {
        private readonly Control owner;
        private IDropTargetHelper helper;
        private bool triedCreate;
        private bool active;

        public DropTargetHelper(Control owner)
        {
            this.owner = owner;
        }

        private IDropTargetHelper Helper
        {
            get
            {
                if (!triedCreate)
                {
                    triedCreate = true;
                    helper = DragSource.CreateTargetHelper();
                }
                return helper;
            }
        }

        private static System.Runtime.InteropServices.ComTypes.IDataObject DataOf(DragEventArgs e)
        {
            return e.Data as System.Runtime.InteropServices.ComTypes.IDataObject;
        }

        public void Enter(DragEventArgs e)
        {
            IDropTargetHelper h = Helper;
            System.Runtime.InteropServices.ComTypes.IDataObject data = DataOf(e);
            if (h == null || data == null)
            {
                return;
            }
            try
            {
                Ole.POINT point = new Ole.POINT(e.X, e.Y);
                h.DragEnter(owner.Handle, data, ref point, (int)e.Effect);
                active = true;
            }
            catch (Exception ex)
            {
                Log.Warn("Drag helper: {0}", ex.Message);
            }
        }

        public void Over(DragEventArgs e)
        {
            if (!active)
            {
                return;
            }
            try
            {
                Ole.POINT point = new Ole.POINT(e.X, e.Y);
                helper.DragOver(ref point, (int)e.Effect);
            }
            catch (Exception ex)
            {
                Log.Warn("Drag helper: {0}", ex.Message);
            }
        }

        public void Leave()
        {
            if (!active)
            {
                return;
            }
            active = false;
            try
            {
                helper.DragLeave();
            }
            catch (Exception ex)
            {
                Log.Warn("Drag helper: {0}", ex.Message);
            }
        }

        public void Drop(DragEventArgs e)
        {
            if (!active)
            {
                return;
            }
            active = false;
            System.Runtime.InteropServices.ComTypes.IDataObject data = DataOf(e);
            try
            {
                Ole.POINT point = new Ole.POINT(e.X, e.Y);
                helper.Drop(data, ref point, (int)e.Effect);
            }
            catch (Exception ex)
            {
                Log.Warn("Drag helper: {0}", ex.Message);
            }
        }

        public void Dispose()
        {
            IDropTargetHelper h = helper;
            helper = null;
            if (h != null)
            {
                Marshal.ReleaseComObject(h);
            }
        }
    }
}
