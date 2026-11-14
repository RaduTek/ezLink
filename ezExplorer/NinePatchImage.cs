using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ezExplorer
{
    /// <summary>
    /// Draws an image stretched to any size while keeping its borders: the four corners are copied as they are,
    /// the edges are stretched in one direction and the centre in both. The border sizes are in source pixels and
    /// are never scaled.
    /// </summary>
    internal sealed class NinePatchImage
    {
        private readonly Image image;
        private readonly int left;
        private readonly int top;
        private readonly int right;
        private readonly int bottom;
        private readonly Rectangle[] src = new Rectangle[9];
        private readonly Rectangle[] dst = new Rectangle[9];

        public NinePatchImage(Image image, int left, int top, int right, int bottom)
        {
            this.image = image;
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
            Rectangle all = new Rectangle(0, 0, image.Width, image.Height);

            // Source rectangles, in the draw order used below: the stretched areas first, the corners last.
            //   5 | 1 | 6
            //   --+---+--
            //   2 | 0 | 4
            //   --+---+--
            //   8 | 3 | 7
            int centerW = all.Width - left - right;
            int centerH = all.Height - top - bottom;
            src[0] = new Rectangle(left, top, centerW, centerH);
            src[1] = new Rectangle(left, 0, centerW, top);
            src[2] = new Rectangle(0, top, left, centerH);
            src[3] = new Rectangle(left, all.Height - bottom, centerW, bottom);
            src[4] = new Rectangle(all.Width - right, top, right, centerH);
            src[5] = new Rectangle(0, 0, left, top);
            src[6] = new Rectangle(all.Width - right, 0, right, top);
            src[7] = new Rectangle(all.Width - right, all.Height - bottom, right, bottom);
            src[8] = new Rectangle(0, all.Height - bottom, left, bottom);
        }

        /// <summary>Draws the image stretched over <paramref name="bounds"/>.</summary>
        public void Draw(Graphics g, Rectangle bounds)
        {
            if (bounds.Width < left + right || bounds.Height < top + bottom)
            {
                return;
            }
            int centerW = bounds.Width - left - right;
            int centerH = bounds.Height - top - bottom;
            int x = bounds.Left;
            int y = bounds.Top;
            dst[0] = new Rectangle(x + left, y + top, centerW, centerH);
            dst[1] = new Rectangle(x + left, y, centerW, top);
            dst[2] = new Rectangle(x, y + top, left, centerH);
            dst[3] = new Rectangle(x + left, y + top + centerH, centerW, bottom);
            dst[4] = new Rectangle(x + left + centerW, y + top, right, centerH);
            dst[5] = new Rectangle(x, y, left, top);
            dst[6] = new Rectangle(x + left + centerW, y, right, top);
            dst[7] = new Rectangle(x + left + centerW, y + top + centerH, right, bottom);
            dst[8] = new Rectangle(x, y + top + centerH, left, bottom);

            InterpolationMode oldInterpolation = g.InterpolationMode;
            PixelOffsetMode oldOffset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            try
            {
                for (int i = 0; i < 9; i++)
                {
                    if (src[i].Width > 0 && src[i].Height > 0 && dst[i].Width > 0 && dst[i].Height > 0)
                    {
                        g.DrawImage(image, dst[i], src[i].X, src[i].Y, src[i].Width, src[i].Height, GraphicsUnit.Pixel);
                    }
                }
            }
            finally
            {
                g.InterpolationMode = oldInterpolation;
                g.PixelOffsetMode = oldOffset;
            }
        }
    }
}
