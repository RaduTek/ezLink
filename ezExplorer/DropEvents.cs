using System;
using System.Drawing;
using System.Windows.Forms;

namespace ezExplorer
{
    /// <summary>Asks whether files may be dropped on a folder; the handler sets <see cref="Allowed"/>.</summary>
    internal sealed class DropQueryEventArgs : EventArgs
    {
        /// <summary>The folder under the cursor, or null for the empty space of the list (the current folder).</summary>
        public readonly string Path;
        public bool Allowed;

        public DropQueryEventArgs(string path)
        {
            Path = path;
        }
    }

    internal sealed class FilesDroppedEventArgs : EventArgs
    {
        public readonly string[] Files;
        /// <summary>The folder dropped on, or null for the empty space of the list (the current folder).</summary>
        public readonly string Path;

        public FilesDroppedEventArgs(string[] files, string path)
        {
            Files = files;
            Path = path;
        }
    }

    /// <summary>The user started dragging the selection out of a pane.</summary>
    internal sealed class DragRequestEventArgs : EventArgs
    {
        public readonly MouseButtons Button;
        /// <summary>For the tree: the folder dragged. Null for the list, which drags its selection.</summary>
        public readonly string Path;

        public DragRequestEventArgs(MouseButtons button, string path)
        {
            Button = button;
            Path = path;
        }
    }

    internal static class DragHelper
    {
        /// <summary>True if the drag carries files from Explorer.</summary>
        public static bool HasFiles(DragEventArgs e)
        {
            return e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop);
        }

        public static string[] GetFiles(DragEventArgs e)
        {
            return e.Data.GetData(DataFormats.FileDrop) as string[];
        }

        /// <summary>Scrolls a list or tree while the cursor is held near its top or bottom edge, as Explorer does.</summary>
        public static void AutoScroll(Control control, Point client)
        {
            const int margin = 20;
            if (client.Y < margin)
            {
                ShellNative.SendMessage(control.Handle, ShellNative.WM_VSCROLL, new IntPtr(ShellNative.SB_LINEUP),
                    IntPtr.Zero);
            }
            else if (client.Y > control.ClientSize.Height - margin)
            {
                ShellNative.SendMessage(control.Handle, ShellNative.WM_VSCROLL, new IntPtr(ShellNative.SB_LINEDOWN),
                    IntPtr.Zero);
            }
        }
    }
}
