using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ezExplorer
{
    /// <summary>
    /// The headline strip at the top of a dialog, Windows 7 style: a left-to-right gradient background with
    /// large, normal-weight text that is left aligned and vertically centered.
    /// </summary>
    public class DialogHeadline : Control
    {
        private Color startColor = Color.FromArgb(0xDC, 0xE5, 0xF4);
        private Color endColor = Color.FromArgb(0x0D, 0x44, 0x69);

        public DialogHeadline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = false;
            Dock = DockStyle.Top;
            Height = 40;
            ForeColor = Color.FromArgb(0x1E, 0x39, 0x5B);
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12f, FontStyle.Regular);
        }

        [DefaultValue(typeof(Color), "220, 229, 244")]
        public Color StartColor
        {
            get { return startColor; }
            set { startColor = value; Invalidate(); }
        }

        [DefaultValue(typeof(Color), "13, 68, 105")]
        public Color EndColor
        {
            get { return endColor; }
            set { endColor = value; Invalidate(); }
        }

        [Browsable(true), EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override string Text
        {
            get { return base.Text; }
            set { base.Text = value; Invalidate(); }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, startColor, endColor, LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            Rectangle textBounds = new Rectangle(Padding.Left + 12, 0, Math.Max(0, bounds.Width - Padding.Horizontal - 24), bounds.Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
}
