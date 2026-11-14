using System;
using System.Drawing;
using System.Windows.Forms;

namespace ezExplorer
{
    /// <summary>
    /// The thin line between the toolbar and the address bar. Its color follows the theme: with visual styles it is
    /// the system shadow color softened towards the toolbar background (the toolbar is light and the full shadow
    /// color would be too harsh against it), without them it is the plain system shadow color.
    /// </summary>
    public class ToolbarDivider : Control
    {
        private const int WM_THEMECHANGED = 0x031A;

        public ToolbarDivider()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Height = 1;
        }

        /// <summary>The line color for the current theme and system colors.</summary>
        public static Color DividerColor
        {
            get
            {
                Color shadow = SystemColors.ControlDark;
                if (!Application.RenderWithVisualStyles)
                {
                    return shadow;
                }
                Color back = SystemColors.Control;
                return Color.FromArgb((shadow.R + back.R) / 2, (shadow.G + back.G) / 2, (shadow.B + back.B) / 2);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (Brush brush = new SolidBrush(DividerColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_THEMECHANGED)
            {
                Invalidate();
            }
        }
    }
}
