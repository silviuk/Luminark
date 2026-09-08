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
                float midX = x + (boxSize / 2.0f);

                // Match the official Luminark logo: Left is dark/black (#1E1E24), Right is bright/white (#FFFFFF)
                Color darkFill = Color.FromArgb(255, 32, 32, 38);
                Color lightFill = Color.FromArgb(255, 255, 255, 255);
                Color outlineColor = isTaskbarLight ? Color.FromArgb(255, 40, 40, 45) : Color.FromArgb(255, 240, 240, 240);

                // 1. Fill Left half with Dark / Black
                using (var leftPath = CreateLeftHalfPath(x, y, boxSize, boxSize, cornerRadius, midX))
                using (var darkBrush = new SolidBrush(darkFill))
                {
                    g.FillPath(darkBrush, leftPath);
                }

                // 2. Fill Right half with Light / White
                using (var rightPath = CreateRightHalfPath(x, y, boxSize, boxSize, cornerRadius, midX))
                using (var lightBrush = new SolidBrush(lightFill))
                {
                    g.FillPath(lightBrush, rightPath);
                }

                // 3. Draw subtle active indicator glow or divider
                Color dividerColor = Color.FromArgb(200, 80, 80, 90);
                using (var dividerPen = new Pen(dividerColor, strokeWidth * 0.75f))
                {
                    g.DrawLine(dividerPen, midX, y, midX, y + boxSize);
                }

                // 4. Draw crisp outer rounded square border
                using (var fullPath = CreateRoundedRectanglePath(x, y, boxSize, boxSize, cornerRadius))
                using (var borderPen = new Pen(outlineColor, strokeWidth))
                {
                    borderPen.StartCap = LineCap.Round;
                    borderPen.EndCap = LineCap.Round;
                    borderPen.LineJoin = LineJoin.Round;
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

        private static GraphicsPath CreateLeftHalfPath(float x, float y, float w, float h, float r, float midX)
        {
            var path = new GraphicsPath();
            float d = r * 2.0f;
            path.AddLine(midX, y, x + r, y);
            path.AddArc(x, y, d, d, 270, -90);
            path.AddLine(x, y + r, x, y + h - r);
            path.AddArc(x, y + h - d, d, d, 180, -90);
            path.AddLine(x + r, y + h, midX, y + h);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateRightHalfPath(float x, float y, float w, float h, float r, float midX)
        {
            var path = new GraphicsPath();
            float d = r * 2.0f;
            path.AddLine(midX, y, x + w - r, y);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddLine(x + w, y + r, x + w, y + h - r);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddLine(x + w - r, y + h, midX, y + h);
            path.CloseFigure();
            return path;
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
