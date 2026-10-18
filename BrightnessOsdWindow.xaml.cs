using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Lumina.Services;

namespace Lumina
{
    public partial class BrightnessOsdWindow : Window
    {
        private readonly DispatcherTimer _hideTimer;
        private bool _isInitializedNative = false;

        public BrightnessOsdWindow()
        {
            InitializeComponent();

            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1300)
            };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                FadeOut();
            };

            SourceInitialized += (s, e) =>
            {
                ApplyWindowStyles();
            };
        }

        private void ApplyWindowStyles()
        {
            if (_isInitializedNative) return;
            var helper = new WindowInteropHelper(this);
            IntPtr handle = helper.Handle;
            if (handle != IntPtr.Zero)
            {
                IntPtr exStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE);
                IntPtr newExStyle = (IntPtr)(exStyle.ToInt64() | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
                NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, newExStyle);
                _isInitializedNative = true;
            }
        }

        public void ShowBrightness(uint brightness)
        {
            OsdProgressBar.Value = brightness;
            OsdPercentageText.Text = $"{brightness}%";

            if (brightness == 0)
                OsdIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.WeatherSunny16;
            else if (brightness < 40)
                OsdIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.BrightnessLow24;
            else if (brightness < 75)
                OsdIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.BrightnessHigh24;
            else
                OsdIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.BrightnessHigh28;

            PositionOnScreen();

            if (!IsVisible)
            {
                Opacity = 0;
                Show();
                ApplyWindowStyles();
            }

            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void FadeOut()
        {
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, e) =>
            {
                if (Opacity == 0)
                {
                    Hide();
                }
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void PositionOnScreen()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Bottom - Height - 50;
        }
    }
}
