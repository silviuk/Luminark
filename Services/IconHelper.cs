using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Lumina.Services
{
    public static class IconHelper
    {
        public static Icon CreateAppIcon()
        {
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Windows 11 Fluent gradient disc
                using (var brush = new LinearGradientBrush(
                    new Point(0, 0),
                    new Point(64, 64),
                    Color.FromArgb(0, 120, 215),
                    Color.FromArgb(138, 90, 246)))
                {
                    g.FillEllipse(brush, 4, 4, 56, 56);
                }

                // Inner luminous sun/moon motif
                using (var glowBrush = new SolidBrush(Color.FromArgb(255, 235, 130)))
                {
                    g.FillEllipse(glowBrush, 16, 16, 32, 32);
                }

                using (var cutoutBrush = new SolidBrush(Color.FromArgb(40, 45, 95)))
                {
                    g.FillEllipse(cutoutBrush, 24, 14, 26, 26);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
    }
}
