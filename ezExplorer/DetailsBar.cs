using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ezExplorer
{
    /// <summary>
    /// The Windows 7 style details bar: a big icon, the bold name and the type on the left, the date modified and
    /// size on the right. Everything is drawn here in one double-buffered pass (no child controls, no background
    /// image), so updating it neither flickers nor lays anything out.
    /// </summary>
    internal sealed class DetailsBar : Control
    {
        private const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis
            | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left
            | TextFormatFlags.NoPadding;

        private static readonly Color BorderColor = Color.FromArgb(157, 182, 197);
        private static readonly Color TopShade = Color.FromArgb(200, 218, 230);
        private static readonly Color LeftColor = Color.FromArgb(246, 252, 255);
        private static readonly Color MiddleColor = Color.FromArgb(236, 251, 254);
        private static readonly Color RightColor = Color.FromArgb(187, 217, 240);

        private Bitmap icon;
        private string name = string.Empty;
        private string type = string.Empty;
        private string modified = string.Empty;
        private string size = string.Empty;
        private string modifiedCaption = string.Empty;
        private string sizeCaption = string.Empty;
        private Font boldFont;

        public DetailsBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            TabStop = false;
            Dock = DockStyle.Bottom;
            Height = 60;
        }

        public string ModifiedCaption
        {
            get { return modifiedCaption; }
            set { modifiedCaption = value ?? string.Empty; Invalidate(); }
        }

        public string SizeCaption
        {
            get { return sizeCaption; }
            set { sizeCaption = value ?? string.Empty; Invalidate(); }
        }

        /// <summary>Shows these details. Redraws only if something actually changed. The icon is not owned.</summary>
        public void SetDetails(Bitmap newIcon, string newName, string newType, string newModified, string newSize)
        {
            if (ReferenceEquals(icon, newIcon) && name == newName && type == newType && modified == newModified
                && size == newSize)
            {
                return;
            }
            icon = newIcon;
            name = newName ?? string.Empty;
            type = newType ?? string.Empty;
            modified = newModified ?? string.Empty;
            size = newSize ?? string.Empty;
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (boldFont != null)
            {
                boldFont.Dispose();
                boldFont = null;
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && boldFont != null)
            {
                boldFont.Dispose();
                boldFont = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }
            PaintBackground(g, bounds);

            float scale = DpiInfo.SystemDpi / 96f;
            int margin = (int)Math.Round(5 * scale);
            int iconMargin = margin + (int)Math.Round(10 * scale);   // space left and right of the icon
            if (icon != null)
            {
                int top = Math.Max(0, (bounds.Height - icon.Height) / 2);
                g.DrawImage(icon, iconMargin, top, icon.Width, icon.Height);
            }
            int textLeft = iconMargin + (int)Math.Round(48 * scale) + iconMargin;

            if (boldFont == null)
            {
                boldFont = new Font(Font, FontStyle.Bold);
            }

            // Two tight lines, centered vertically as a block.
            int rowHeight = Font.Height;
            int rowSpacing = 6;
            int row1 = Math.Max(0, (bounds.Height - 2 * rowHeight - rowSpacing) / 2);
            int row2 = row1 + rowHeight + rowSpacing;
            int right = bounds.Width - margin - 3;

            // Right block: captions (gray) and values; the value column keeps a minimum width so it does not jump.
            int valueWidth = (int)Math.Round(130 * scale);
            valueWidth = Math.Max(valueWidth, Math.Max(Measure(modified, Font), Measure(size, Font)));
            int valueLeft = right - valueWidth;
            int captionWidth = Math.Max(
                modified.Length > 0 ? Measure(modifiedCaption, Font) : 0,
                size.Length > 0 ? Measure(sizeCaption, Font) : 0);
            int captionLeft = valueLeft - (int)Math.Round(6 * scale) - captionWidth;

            int nameRight = bounds.Width - margin;
            if (captionWidth > 0 || valueWidth > 0)
            {
                nameRight = captionLeft - (int)Math.Round(12 * scale);
            }
            if (nameRight < textLeft)
            {
                nameRight = textLeft;
            }

            DrawText(g, name, boldFont, SystemColors.ControlText, new Rectangle(textLeft, row1, nameRight - textLeft, rowHeight));
            DrawText(g, type, Font, SystemColors.GrayText, new Rectangle(textLeft, row2, nameRight - textLeft, rowHeight));

            if (modified.Length > 0 && valueLeft > textLeft)
            {
                DrawText(g, modifiedCaption, Font, SystemColors.GrayText, new Rectangle(captionLeft, row1, captionWidth, rowHeight), true);
                DrawText(g, modified, Font, SystemColors.ControlText, new Rectangle(valueLeft, row1, valueWidth, rowHeight));
            }
            if (size.Length > 0 && valueLeft > textLeft)
            {
                DrawText(g, sizeCaption, Font, SystemColors.GrayText, new Rectangle(captionLeft, row2, captionWidth, rowHeight), true);
                DrawText(g, size, Font, SystemColors.ControlText, new Rectangle(valueLeft, row2, valueWidth, rowHeight));
            }
        }

        private static void PaintBackground(Graphics g, Rectangle bounds)
        {
            // Horizontal blue fade, as in the Windows 7 details pane.
            if (bounds.Width > 1)
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(bounds, LeftColor, RightColor, LinearGradientMode.Horizontal))
                {
                    ColorBlend blend = new ColorBlend(3);
                    blend.Colors = new Color[] { LeftColor, MiddleColor, RightColor };
                    blend.Positions = new float[] { 0f, 0.5f, 1f };
                    brush.InterpolationColors = blend;
                    g.FillRectangle(brush, bounds);
                }
            }
            else
            {
                g.Clear(LeftColor);
            }

            // Darker top edge fading out over a few pixels, then a 1 pixel border line.
            int shade = Math.Min(10, bounds.Height);
            Rectangle top = new Rectangle(bounds.Left, bounds.Top, bounds.Width, shade);
            using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(top.Left, top.Top, top.Width, shade + 1),
                Color.FromArgb(255, TopShade), Color.FromArgb(0, TopShade), LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, top);
            }
            using (Pen pen = new Pen(BorderColor))
            {
                g.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
            }
        }

        private static int Measure(string text, Font font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            return TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            DrawText(g, text, font, color, bounds, false);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, bool rightAlign)
        {
            if (text.Length > 0 && bounds.Width > 0 && bounds.Height > 0)
            {
                TextFormatFlags flags = rightAlign ? (TextFlags & ~TextFormatFlags.Left) | TextFormatFlags.Right : TextFlags;
                TextRenderer.DrawText(g, text, font, bounds, color, flags);
            }
        }
    }
}
