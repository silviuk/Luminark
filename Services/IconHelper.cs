using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Lumina.Services
{
    public static class IconHelper
    {
        public static Icon CreateAppIcon()
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

                float padding = Math.Max(1.0f, size * 0.05f);
                float w = size - (2 * padding);

                // Rounded base with dark midnight slate gradient
                using var path = new GraphicsPath();
                var rect = new RectangleF(padding, padding, w, w);
                path.AddEllipse(rect);

                using (var bgBrush = new LinearGradientBrush(rect,
                    Color.FromArgb(255, 24, 26, 40),
                    Color.FromArgb(255, 12, 14, 24),
                    45.0f))
                {
                    g.FillPath(bgBrush, path);
                }

                using (var borderPen = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(1.0f, size * 0.04f)))
                {
                    g.DrawPath(borderPen, path);
                }

                // Radiant golden sun
                float sunSize = w * 0.58f;
                float sunX = padding + (w * 0.12f);
                float sunY = padding + (w * 0.20f);
                var sunRect = new RectangleF(sunX, sunY, sunSize, sunSize);

                using (var sunBrush = new LinearGradientBrush(sunRect,
                    Color.FromArgb(255, 255, 215, 60),
                    Color.FromArgb(255, 255, 145, 0),
                    90.0f))
                {
                    g.FillEllipse(sunBrush, sunRect);
                }

                // Sleek crescent moon cutout
                float moonSize = w * 0.52f;
                float moonX = sunX + (sunSize * 0.32f);
                float moonY = sunY - (sunSize * 0.08f);
                var moonRect = new RectangleF(moonX, moonY, moonSize, moonSize);

                using (var moonBrush = new LinearGradientBrush(moonRect,
                    Color.FromArgb(255, 30, 32, 48),
                    Color.FromArgb(255, 14, 16, 26),
                    45.0f))
                {
                    g.FillEllipse(moonBrush, moonRect);
                }

                // Cyan luminous star accent
                float accentSize = Math.Max(2.0f, w * 0.18f);
                float accentX = padding + (w * 0.68f);
                float accentY = padding + (w * 0.60f);
                var accentRect = new RectangleF(accentX, accentY, accentSize, accentSize);

                using (var accentBrush = new LinearGradientBrush(accentRect,
                    Color.FromArgb(255, 96, 205, 255),
                    Color.FromArgb(255, 0, 120, 215),
                    45.0f))
                {
                    g.FillEllipse(accentBrush, accentRect);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
    }
}
