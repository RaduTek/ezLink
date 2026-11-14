using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EzLink;
using ezExplorer.NativeToolStrip;

namespace ezExplorer
{
    internal delegate void PathHandler(string path);

    /// <summary>
    /// The Windows 7 style address bar: the icon of the current folder, then one clickable crumb per path segment,
    /// each followed by an arrow that drops a menu of the folders inside it. Clicking the empty space turns the bar
    /// into a text box for typing a path.
    ///
    /// Everything is drawn here from system colors and the breadcrumb images: no visual style (uxtheme) drawing is
    /// used, so the bar looks the same with and without visual styles, on every Windows version from XP.
    /// </summary>
    internal sealed class BreadcrumbBar : Control
    {
        private const TextFormatFlags LabelFlags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis
            | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left
            | TextFormatFlags.NoPadding;

        private const int WM_THEMECHANGED = 0x031A;

        private enum Part
        {
            None,
            Label,
            Arrow,
            Overflow
        }

        private sealed class Crumb
        {
            public readonly string Path;
            public readonly string Label;
            public int TextWidth;
            public Rectangle LabelRect;
            public Rectangle ArrowRect;

            public Crumb(string path, string label)
            {
                Path = path;
                Label = label;
            }
        }

        // The crumb backgrounds are shared by all bars. They are sliced with 2 pixel borders.
        private static NinePatchImage normalPatch;
        private static NinePatchImage hoverPatch;
        private static NinePatchImage pressedPatch;
        private static NinePatchImage disabledPatch;

        private readonly List<Crumb> crumbs = new List<Crumb>();
        private RemoteBrowser browser;
        private string rootText = string.Empty;
        private string path;

        // Layout (valid when layoutValid).
        private bool layoutValid;
        private Rectangle iconRect;
        private Rectangle overflowRect;
        private int hiddenCount;

        private int hotCrumb = -1;
        private Part hotPart = Part.None;
        private int pressedCrumb = -1;
        private Part pressedPart = Part.None;

        private int generation;               // drops folder listings that arrive after a newer request
        private NativeContextMenuStrip menu;
        private Font menuBoldFont;

        private TextBox editBox;
        private bool editing;

        /// <summary>Raised when a crumb or a menu item is clicked.</summary>
        public event PathHandler NavigateRequested;

        /// <summary>Raised when Enter is pressed while editing. The text is what was typed.</summary>
        public event PathHandler PathSubmitted;

        /// <summary>Raised when editing is cancelled with Escape.</summary>
        public event EventHandler EditCancelled;

        public BreadcrumbBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            RebuildCrumbs();
        }

        /// <summary>Where the sub-folder menus get their folders from.</summary>
        public RemoteBrowser Browser
        {
            get { return browser; }
            set { browser = value; }
        }

        /// <summary>The name of the computer level (the peer), shown for the root.</summary>
        public string RootText
        {
            get { return rootText; }
            set
            {
                rootText = value ?? string.Empty;
                RebuildCrumbs();
            }
        }

        /// <summary>The height this bar needs for its icon and text, at the current DPI.</summary>
        public int PreferredHeight
        {
            get
            {
                float scale = DpiScale;
                int content = Math.Max(Math.Max(ShellIcons.SmallIconSize.Height + Round(4 * scale), Font.Height + Round(4 * scale)),
                    Round(20 * scale));
                return content + 2 * BorderThickness;
            }
        }

        /// <summary>Shows this path and leaves edit mode.</summary>
        public void ShowPath(string newPath)
        {
            path = newPath;
            EndEdit();
            RebuildCrumbs();
        }

        /// <summary>Turns the bar into a text box with the current path selected.</summary>
        public void BeginEdit()
        {
            if (editing || !Enabled || IsDisposed)
            {
                return;
            }
            CancelMenu();
            if (editBox == null)
            {
                editBox = new TextBox();
                editBox.BorderStyle = BorderStyle.None;
                editBox.Visible = false;
                editBox.KeyDown += OnEditKeyDown;
                editBox.Leave += OnEditLeave;
                Controls.Add(editBox);
            }
            editing = true;
            editBox.Text = path == null || RemotePaths.IsRoot(path) ? rootText : path;
            PositionEditBox();
            editBox.Visible = true;
            editBox.Focus();
            editBox.SelectAll();
            Invalidate();
        }

        // ---- metrics ----

        private static float DpiScale
        {
            get { return DpiInfo.SystemDpi / 96f; }
        }

        private static int LabelPadLeft
        {
            get { return Round(4 * DpiScale); }
        }

        private static int LabelPadRight
        {
            get { return Round(-2 * DpiScale); }
        }

        private static int Round(float value)
        {
            return (int)Math.Round(value);
        }

        /// <summary>1 pixel flat border with visual styles, the classic 2 pixel sunken edge without.</summary>
        private static int BorderThickness
        {
            get { return Application.RenderWithVisualStyles ? 1 : 2; }
        }

        private Rectangle InnerRect()
        {
            int t = BorderThickness;
            return new Rectangle(t, t, Math.Max(0, ClientSize.Width - 2 * t), Math.Max(0, ClientSize.Height - 2 * t));
        }

        private static Bitmap ArrowImage
        {
            get
            {
                return DpiInfo.HighDpi ? Properties.Resources.breadcrumb_arrow_large : Properties.Resources.breadcrumb_arrow;
            }
        }

        private void InvalidateLayout()
        {
            layoutValid = false;
            Invalidate();
        }

        private void RebuildCrumbs()
        {
            crumbs.Clear();
            crumbs.Add(new Crumb(RemotePaths.Root, rootText));
            if (path != null && !RemotePaths.IsRoot(path))
            {
                foreach (string segment in RemotePaths.Chain(path))
                {
                    string name = RemotePaths.GetName(segment);
                    if (RemotePaths.IsDriveRoot(segment))
                    {
                        name = name.TrimEnd('\\');
                    }
                    crumbs.Add(new Crumb(segment, name));
                }
            }
            hotCrumb = -1;
            hotPart = Part.None;
            InvalidateLayout();
        }

        private void EnsureLayout()
        {
            if (layoutValid)
            {
                return;
            }
            layoutValid = true;

            float scale = DpiScale;
            Rectangle inner = InnerRect();
            int pad = Round(4 * scale);
            int labelPad = LabelPadLeft + LabelPadRight;
            int arrowWidth = ArrowImage.Width + 2 * Round(5 * scale);
            Size iconSize = ShellIcons.SmallIconSize;

            iconRect = new Rectangle(inner.Left + pad, inner.Top + (inner.Height - iconSize.Height) / 2,
                iconSize.Width, iconSize.Height);
            int start = iconRect.Right + pad;
            int available = inner.Right - pad - start;

            int total = 0;
            foreach (Crumb crumb in crumbs)
            {
                crumb.TextWidth = TextRenderer.MeasureText(crumb.Label, Font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                total += crumb.TextWidth + labelPad + arrowWidth - 1;
            }

            // Hide the leftmost crumbs until the rest fit; the last one always stays.
            hiddenCount = 0;
            while (hiddenCount < crumbs.Count - 1 && total + (hiddenCount > 0 ? arrowWidth : 0) > available)
            {
                Crumb hidden = crumbs[hiddenCount];
                total -= hidden.TextWidth + labelPad + arrowWidth - 1;
                hiddenCount++;
            }

            int x = start;
            if (hiddenCount > 0)
            {
                overflowRect = new Rectangle(x, inner.Top, arrowWidth, inner.Height);
                x += arrowWidth;
            }
            else
            {
                overflowRect = Rectangle.Empty;
            }
            for (int i = hiddenCount; i < crumbs.Count; i++)
            {
                Crumb crumb = crumbs[i];
                int labelWidth = crumb.TextWidth + labelPad;
                if (i == crumbs.Count - 1)
                {
                    // The last crumb gives way when even it does not fit.
                    labelWidth = Math.Max(Math.Min(labelWidth, labelPad + Round(16 * scale)),
                        Math.Min(labelWidth, inner.Right - pad - x - arrowWidth + 1));
                }
                crumb.LabelRect = new Rectangle(x, inner.Top, labelWidth, inner.Height);
                x += labelWidth - 1; // the arrow button overlaps the label by 1px, so the two share one border
                crumb.ArrowRect = new Rectangle(x, inner.Top, arrowWidth, inner.Height);
                x += arrowWidth;
            }
        }

        private void PositionEditBox()
        {
            EnsureLayout();
            Rectangle inner = InnerRect();
            int pad = Round(4 * DpiScale);
            int x = iconRect.Right + pad;
            int height = editBox.PreferredHeight;
            editBox.SetBounds(x, inner.Top + Math.Max(0, (inner.Height - height) / 2),
                Math.Max(0, inner.Right - pad - x), height);
        }

        // ---- painting ----

        private static void EnsurePatches()
        {
            if (normalPatch != null)
            {
                return;
            }
            hoverPatch = new NinePatchImage(Properties.Resources.breadcrumb_aero_hover, 2, 2, 2, 2);
            pressedPatch = new NinePatchImage(Properties.Resources.breadcrumb_aero_pressed, 2, 2, 2, 2);
            disabledPatch = new NinePatchImage(Properties.Resources.breadcrumb_aero_disabled, 2, 2, 2, 2);
            normalPatch = new NinePatchImage(Properties.Resources.breadcrumb_aero_normal, 2, 2, 2, 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureLayout();
            EnsurePatches();
            Graphics g = e.Graphics;
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (Brush back = new SolidBrush(Enabled ? SystemColors.Window : SystemColors.Control))
            {
                g.FillRectangle(back, bounds);
            }
            PaintBorder(g, bounds);
            ShellIcons.DrawSmall(g, IconIndex(), iconRect.X, iconRect.Y);
            if (editing)
            {
                return;
            }

            Rectangle inner = InnerRect();
            bool pressedNow = pressedPart != Part.None;
            int activeCrumb = pressedNow ? pressedCrumb : hotCrumb;
            Part activePart = pressedNow ? pressedPart : hotPart;
            Color textColor = Enabled ? SystemColors.WindowText : SystemColors.GrayText;
            Bitmap arrow = ArrowImage;

            if (hiddenCount > 0)
            {
                if (!Enabled)
                {
                    PaintCell(g, overflowRect, inner, disabledPatch);
                }
                else if (activePart == Part.Overflow)
                {
                    PaintCell(g, overflowRect, inner, pressedNow ? pressedPatch : hoverPatch);
                }
                TextRenderer.DrawText(g, "«", Font, overflowRect, textColor,
                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }

            for (int i = hiddenCount; i < crumbs.Count; i++)
            {
                Crumb crumb = crumbs[i];
                if (!Enabled)
                {
                    PaintCell(g, crumb.LabelRect, inner, disabledPatch);
                    PaintCell(g, crumb.ArrowRect, inner, disabledPatch);
                }
                else if (i == activeCrumb && (activePart == Part.Label || activePart == Part.Arrow))
                {
                    // The part under the mouse (or pressed) gets its own look, the other part of the same crumb the
                    // normal one. Crumbs that are not under the mouse have no background at all.
                    PaintCell(g, crumb.LabelRect, inner,
                        activePart == Part.Label ? (pressedNow ? pressedPatch : hoverPatch) : normalPatch);
                    PaintCell(g, crumb.ArrowRect, inner,
                        activePart == Part.Arrow ? (pressedNow ? pressedPatch : hoverPatch) : normalPatch);
                }

                Rectangle text = new Rectangle(crumb.LabelRect.Left + LabelPadLeft, crumb.LabelRect.Top,
                    Math.Max(0, crumb.LabelRect.Width - LabelPadLeft - LabelPadRight), crumb.LabelRect.Height);
                TextRenderer.DrawText(g, crumb.Label, Font, text, textColor, LabelFlags);

                g.DrawImage(arrow, crumb.ArrowRect.Left + (crumb.ArrowRect.Width - arrow.Width) / 2,
                    inner.Top + (inner.Height - arrow.Height) / 2, arrow.Width, arrow.Height);
            }
        }

        /// <summary>
        /// Draws a crumb cell with its whole image, including the top and bottom border rows, which are drawn over
        /// the border of the bar (one pixel above and below the inner area).
        /// </summary>
        private static void PaintCell(Graphics g, Rectangle cell, Rectangle inner, NinePatchImage patch)
        {
            patch.Draw(g, new Rectangle(cell.Left, inner.Top - 1, cell.Width, inner.Height + 2));
        }

        private static void PaintBorder(Graphics g, Rectangle bounds)
        {
            int w = bounds.Width;
            int h = bounds.Height;
            if (Application.RenderWithVisualStyles)
            {
                using (Pen pen = new Pen(SystemColors.ControlDark))
                {
                    g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
                }
                return;
            }

            // Classic sunken edge.
            using (Pen shadow = new Pen(SystemColors.ControlDark))
            using (Pen dark = new Pen(SystemColors.ControlDarkDark))
            using (Pen highlight = new Pen(SystemColors.ControlLightLight))
            using (Pen light = new Pen(SystemColors.ControlLight))
            {
                g.DrawLine(shadow, 0, 0, w - 2, 0);
                g.DrawLine(shadow, 0, 0, 0, h - 2);
                g.DrawLine(highlight, 0, h - 1, w - 1, h - 1);
                g.DrawLine(highlight, w - 1, 0, w - 1, h - 1);
                g.DrawLine(dark, 1, 1, w - 3, 1);
                g.DrawLine(dark, 1, 1, 1, h - 3);
                g.DrawLine(light, 1, h - 2, w - 2, h - 2);
                g.DrawLine(light, w - 2, 1, w - 2, h - 2);
            }
        }

        private int IconIndex()
        {
            return CrumbIconIndex(path);
        }

        private static int CrumbIconIndex(string crumbPath)
        {
            return ShellIcons.ForPath(crumbPath);
        }

        // ---- mouse ----

        private bool HitTest(Point point, out int crumbIndex, out Part part)
        {
            EnsureLayout();
            crumbIndex = -1;
            part = Part.None;
            if (hiddenCount > 0 && overflowRect.Contains(point))
            {
                part = Part.Overflow;
                return true;
            }
            for (int i = hiddenCount; i < crumbs.Count; i++)
            {
                Crumb crumb = crumbs[i];
                if (crumb.LabelRect.Contains(point))
                {
                    crumbIndex = i;
                    part = Part.Label;
                    return true;
                }
                if (crumb.ArrowRect.Contains(point))
                {
                    crumbIndex = i;
                    part = Part.Arrow;
                    return true;
                }
            }
            return false;
        }

        private void UpdateHot(Point point)
        {
            int index;
            Part part;
            if (!HitTest(point, out index, out part))
            {
                index = -1;
                part = Part.None;
            }
            if (index != hotCrumb || part != hotPart)
            {
                hotCrumb = index;
                hotPart = part;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!editing && Enabled)
            {
                UpdateHot(e.Location);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hotPart != Part.None)
            {
                hotCrumb = -1;
                hotPart = Part.None;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || editing || !Enabled)
            {
                return;
            }
            int index;
            Part part;
            if (!HitTest(e.Location, out index, out part))
            {
                BeginEdit();
                return;
            }

            CancelMenu();
            pressedCrumb = index;
            pressedPart = part;
            Invalidate();
            if (part == Part.Arrow)
            {
                OpenFolderMenu(index);
            }
            else if (part == Part.Overflow)
            {
                ShowOverflowMenu();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || pressedPart != Part.Label)
            {
                return;
            }
            int pressed = pressedCrumb;
            pressedCrumb = -1;
            pressedPart = Part.None;
            Invalidate();

            int index;
            Part part;
            if (pressed >= 0 && pressed < crumbs.Count && HitTest(e.Location, out index, out part)
                && index == pressed && part == Part.Label)
            {
                RaiseNavigate(crumbs[pressed].Path);
            }
        }

        private void RaiseNavigate(string target)
        {
            PathHandler handler = NavigateRequested;
            if (handler != null)
            {
                handler(target);
            }
        }

        // ---- menus ----

        private void OpenFolderMenu(int crumbIndex)
        {
            if (browser == null)
            {
                ClearPressed();
                return;
            }
            int ticket = ++generation;
            browser.ListFolders(crumbs[crumbIndex].Path, delegate(List<RemoteItem> folders, Exception error)
            {
                if (ticket != generation || IsDisposed)
                {
                    return;
                }
                if (error != null)
                {
                    ClearPressed();
                    return;
                }
                ShowFolderMenu(crumbIndex, folders);
            });
        }

        private void ShowFolderMenu(int crumbIndex, List<RemoteItem> folders)
        {
            if (crumbIndex >= crumbs.Count)
            {
                ClearPressed();
                return;
            }
            DisposeMenu();
            NativeContextMenuStrip strip = new NativeContextMenuStrip();
            strip.ImageScalingSize = ShellIcons.SmallIconSize;
            string next = crumbIndex + 1 < crumbs.Count ? crumbs[crumbIndex + 1].Path : null;
            if (folders == null || folders.Count == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem(Strings.Get("BreadcrumbEmpty"));
                empty.Enabled = false;
                strip.Items.Add(empty);
            }
            else
            {
                foreach (RemoteItem folder in folders)
                {
                    string text = string.IsNullOrEmpty(folder.DisplayName) ? folder.Name : folder.DisplayName;
                    ToolStripMenuItem item = new ToolStripMenuItem(text.Replace("&", "&&"));
                    item.Tag = folder.Path;
                    item.Image = ShellIcons.SmallIcon(folder.IconIndex);
                    if (next != null && RemotePaths.SamePath(next, folder.Path))
                    {
                        item.Font = MenuBoldFont;
                    }
                    item.Click += OnMenuItemClick;
                    strip.Items.Add(item);
                }
            }
            ShowMenu(strip, crumbs[crumbIndex].ArrowRect);
        }

        private void ShowOverflowMenu()
        {
            DisposeMenu();
            NativeContextMenuStrip strip = new NativeContextMenuStrip();
            strip.ImageScalingSize = ShellIcons.SmallIconSize;
            for (int i = 0; i < hiddenCount; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(crumbs[i].Label.Replace("&", "&&"));
                item.Tag = crumbs[i].Path;
                item.Image = ShellIcons.SmallIcon(CrumbIconIndex(crumbs[i].Path));
                item.Click += OnMenuItemClick;
                strip.Items.Add(item);
            }
            ShowMenu(strip, overflowRect);
        }

        private void ShowMenu(NativeContextMenuStrip strip, Rectangle below)
        {
            menu = strip;
            strip.Closed += OnMenuClosed;
            strip.Show(this, new Point(below.Left, ClientSize.Height - BorderThickness));
        }

        private Font MenuBoldFont
        {
            get
            {
                if (menuBoldFont == null)
                {
                    menuBoldFont = new Font(SystemFonts.MenuFont, FontStyle.Bold);
                }
                return menuBoldFont;
            }
        }

        private void OnMenuItemClick(object sender, EventArgs e)
        {
            string target = ((ToolStripItem)sender).Tag as string;
            if (target != null)
            {
                RaiseNavigate(target);
            }
        }

        private void OnMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            ClearPressed();
            if (IsHandleCreated && !editing)
            {
                UpdateHot(PointToClient(Cursor.Position));
            }
        }

        private void ClearPressed()
        {
            pressedCrumb = -1;
            pressedPart = Part.None;
            Invalidate();
        }

        /// <summary>Forgets a menu that is open or still being listed.</summary>
        private void CancelMenu()
        {
            generation++;
            if (menu != null && menu.Visible)
            {
                menu.Close();
            }
        }

        private void DisposeMenu()
        {
            if (menu != null)
            {
                menu.Closed -= OnMenuClosed;
                menu.Dispose();
                menu = null;
            }
        }

        // ---- editing ----

        private void EndEdit()
        {
            if (!editing)
            {
                return;
            }
            editing = false;
            editBox.Visible = false;
            Invalidate();
        }

        private void OnEditKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string text = editBox.Text;
                EndEdit();
                PathHandler handler = PathSubmitted;
                if (handler != null)
                {
                    handler(text);
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                EndEdit();
                EventHandler handler = EditCancelled;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        private void OnEditLeave(object sender, EventArgs e)
        {
            EndEdit();
        }

        // ---- system changes ----

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            InvalidateLayout();
            if (editing)
            {
                PositionEditBox();
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            InvalidateLayout();
            if (editing)
            {
                PositionEditBox();
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled)
            {
                EndEdit();
                CancelMenu();
                hotCrumb = -1;
                hotPart = Part.None;
            }
            Invalidate();
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            InvalidateLayout();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_THEMECHANGED)
            {
                // Visual styles switched on or off: the border thickness changes with them.
                InvalidateLayout();
                if (editing)
                {
                    PositionEditBox();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                generation++;
                DisposeMenu();
                if (menuBoldFont != null)
                {
                    menuBoldFont.Dispose();
                    menuBoldFont = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
