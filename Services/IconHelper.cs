using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Lumina.Services
{
    public static class IconHelper
    {
        /// <summary>
        /// Creates a modern, bold Windows 11 style rounded square (squircle) icon,
        /// split vertically down the middle into half-dark and half-light.
        /// 
        /// In Light Mode:
        ///   The right half is solid filled (indicating active bright/day on the right),
        ///   left half is empty/dark with crisp border.
        /// In Dark Mode:
        ///   The left half is solid filled (indicating active night/dark on the left),
        ///   right half is empty with crisp border.
        ///
        /// High contrast adaptation:
        ///   On Dark Taskbar: outline and bright fill are pure white (#FFFFFF), dark side is transparent/void.
        ///   On Light Taskbar: outline and dark fill are deep charcoal (#181818), light side is clean.
        /// </summary>
        public static Icon CreateDynamicTrayIcon(bool isLightMode, bool isTaskbarLight)
        {
            int size = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
            if (size < 16) size = 32;

            using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                Color fgColor = isTaskbarLight
                    ? Color.FromArgb(255, 20, 20, 20)      // Crisp charcoal on light taskbar
                    : Color.FromArgb(255, 255, 255, 255);    // Pure white on dark taskbar

                // Use maximum size with a clean 2px margin for a big, prominent look
                float padding = Math.Max(1.0f, size * 0.08f);
                float boxSize = size - (2.0f * padding);
                float x = padding;
                float y = padding;
                float cornerRadius = boxSize * 0.28f; // Smooth modern Windows 11 squircle curvature
                float strokeWidth = Math.Max(1.5f, size * 0.09f);

                using var fullPath = CreateRoundedRectanglePath(x, y, boxSize, boxSize, cornerRadius);

                // 1. Fill the active half inside the rounded square
                // Clip rendering to the inside of the rounded square path
                var origClip = g.Clip;
                using (var pathRegion = new Region(fullPath))
                {
                    g.Clip = pathRegion;

                    float midX = x + (boxSize / 2.0f);
                    RectangleF activeHalfRect;

                    if (isLightMode)
                    {
                        // Light Mode: Right half is solidly illuminated
                        activeHalfRect = new RectangleF(midX, y, (x + boxSize) - midX, boxSize);
                    }
                    else
                    {
                        // Dark Mode: Left half is solidly filled (or vice versa)
                        activeHalfRect = new RectangleF(x, y, midX - x, boxSize);
                    }

                    using (var fillBrush = new SolidBrush(fgColor))
                    {
                        g.FillRectangle(fillBrush, activeHalfRect);
                    }

                    // Reset clip
                    g.Clip = origClip;
                }

                // 2. Draw the vertical dividing line down the exact center
                using (var dividerPen = new Pen(fgColor, strokeWidth))
                {
                    float midX = (float)Math.Round(x + (boxSize / 2.0f));
                    g.DrawLine(dividerPen, midX, y, midX, y + boxSize);
                }

                // 3. Draw the crisp outer rounded square border
                using (var borderPen = new Pen(fgColor, strokeWidth))
                {
                    borderPen.Alignment = PenAlignment.Inset;
                    g.DrawPath(borderPen, fullPath);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        public static Icon CreateAppIcon()
        {
            return CreateDynamicTrayIcon(isLightMode: false, isTaskbarLight: false);
        }

        private static GraphicsPath CreateRoundedRectanglePath(float x, float y, float width, float height, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2.0f;

            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
