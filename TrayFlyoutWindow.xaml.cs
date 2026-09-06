using System;
using System.Windows;
using System.Windows.Media;
using Lumina.Services;
using Lumina.ViewModels;

namespace Lumina
{
    public partial class TrayFlyoutWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly TrayScrollHook _scrollHook;
        private readonly Action _openDashboardCallback;

        internal TrayFlyoutWindow(MainViewModel viewModel, TrayScrollHook scrollHook, Action openDashboardCallback)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _scrollHook = scrollHook;
            _openDashboardCallback = openDashboardCallback;
            DataContext = _viewModel;

            Deactivated += (s, e) =>
            {
                if (IsVisible)
                {
                    Hide();
                    _viewModel.CancelLockCountdown();
                    MainWindow.TrimMemory();
                }
            };
        }

        public void ShowNearTray()
        {
            UpdatePosition();
            Show();
            Activate();
            Focus();
        }

        public void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
                _viewModel.CancelLockCountdown();
                MainWindow.TrimMemory();
            }
            else
            {
                ShowNearTray();
            }
        }

        private void UpdatePosition()
        {
            double scale = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                scale = source.CompositionTarget.TransformToDevice.M11;
            }

            var workArea = SystemParameters.WorkArea;
            double windowWidth = Width;
            double windowHeight = ActualHeight > 0 ? ActualHeight : 440;

            if (_scrollHook.TryGetTrayIconRect(out var rect))
            {
                double iconLeft = rect.Left / scale;
                double iconRight = rect.Right / scale;
                double iconTop = rect.Top / scale;
                double iconBottom = rect.Bottom / scale;

                // Center horizontally over tray icon
                double left = iconLeft - (windowWidth / 2.0) + ((iconRight - iconLeft) / 2.0);
                double top = iconTop - windowHeight - 10;

                // If taskbar is on top
                if (top < workArea.Top)
                {
                    top = iconBottom + 10;
                }

                // Keep inside screen bounds
                if (left + windowWidth > workArea.Right - 8)
                {
                    left = workArea.Right - windowWidth - 8;
                }
                if (left < workArea.Left + 8)
                {
                    left = workArea.Left + 8;
                }
                if (top + windowHeight > workArea.Bottom - 8)
                {
                    top = workArea.Bottom - windowHeight - 8;
                }

                Left = left;
                Top = top;
            }
            else
            {
                // Fallback to cursor position
                NativeMethods.GetCursorPos(out var pt);
                double curX = pt.x / scale;
                double curY = pt.y / scale;

                double left = Math.Min(curX - (windowWidth / 2), workArea.Right - windowWidth - 12);
                double top = Math.Max(workArea.Top + 12, curY - windowHeight - 12);

                Left = Math.Max(workArea.Left + 12, left);
                Top = top;
            }
        }

        private void OnToggleThemeClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleTheme();
        }

        private void OnOpenFullDashboardClicked(object sender, RoutedEventArgs e)
        {
            Hide();
            _openDashboardCallback();
        }

        private void OnToggleLinkMonitorsClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleMonitorsLinked();
        }

        private void OnLockAndTurnOffClicked(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsLockingCountdown)
            {
                _viewModel.CancelLockCountdown();
            }
            else
            {
                _viewModel.StartLockAndTurnOff();
            }
        }

        private void OnPresetClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is string tagStr && uint.TryParse(tagStr, out uint val))
            {
                _viewModel.MasterBrightness = val;
            }
        }
    }
}
