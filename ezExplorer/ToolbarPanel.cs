using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace ezExplorer
{
    /// <summary>
    /// A panel whose background is painted like the background of a NativeToolStrip with the Toolbar theme (the
    /// Rebar background of the current visual style). Without visual styles it keeps the plain control color.
    /// </summary>
    public class ToolbarPanel : Panel
    {
        private const string RebarClass = "Rebar";
        private const int RebarBackground = 6;
        private const string MenuClass = "Menu";
        private const int MenuBarBackground = 7;
        private const int MenuBarActive = 1;

        private bool overpaintBackground;

        public ToolbarPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// The Windows 7 toolbar background has a gradient in its top half. When this is set, the background is
        /// painted at twice the height of the panel and clipped to the panel, so that the panel shows the bottom
        /// half only (without the gradient), however low the panel is.
        /// </summary>
        [DefaultValue(false)]
        [Category("Appearance")]
        [Description("Paints the toolbar background at double height and clips it, showing only its bottom half.")]
        public bool OverpaintBackground
        {
            get { return overpaintBackground; }
            set
            {
                if (overpaintBackground != value)
                {
                    overpaintBackground = value;
                    Invalidate();
                }
            }
        }

        private static bool IsSupported
        {
            get
            {
                // The same check the NativeToolStripRenderer makes.
                return VisualStyleRenderer.IsSupported
                    && VisualStyleRenderer.IsElementDefined(VisualStyleElement.CreateElement(MenuClass, MenuBarBackground, MenuBarActive));
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (!IsSupported)
            {
                base.OnPaintBackground(e);
                return;
            }

            VisualStyleRenderer renderer;
            if (VisualStyleRenderer.IsElementDefined(VisualStyleElement.CreateElement(RebarClass, RebarBackground, 0)))
            {
                renderer = new VisualStyleRenderer(RebarClass, RebarBackground, 0);
            }
            else
            {
                renderer = new VisualStyleRenderer(RebarClass, 0, 0);
            }

            Rectangle client = ClientRectangle;
            Rectangle clip = Rectangle.Intersect(e.ClipRectangle, client);
            if (clip.IsEmpty)
            {
                return;
            }
            if (renderer.IsBackgroundPartiallyTransparent())
            {
                renderer.DrawParentBackground(e.Graphics, clip, this);
            }

            Rectangle bounds = client;
            if (overpaintBackground)
            {
                // Double height, moved up by the panel height: the panel shows the bottom half.
                bounds.Y = -client.Height;
                bounds.Height = client.Height * 2;
            }
            renderer.DrawBackground(e.Graphics, bounds, clip);
        }
    }
}
