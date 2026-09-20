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
                    _viewModel.CancelAllInputSwitches();
                    MainWindow.TrimMemory();
                }
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    if (_viewModel.HasActiveInputSwitch)
                    {
                        _viewModel.CancelAllInputSwitches();
                        e.Handled = true;
                        return;
                    }
                    if (_viewModel.IsLockingCountdown)
                    {
                        _viewModel.CancelLockCountdown();
                        e.Handled = true;
                        return;
                    }
                    Hide();
                    _viewModel.CancelLockCountdown();
                    _viewModel.CancelAllInputSwitches();
                    MainWindow.TrimMemory();
                    e.Handled = true;
                }
            };

            PreviewMouseWheel += (s, e) =>
            {
                int step = e.Delta > 0 ? 5 : -5;
                long newBright = (long)_viewModel.MasterBrightness + step;
                _viewModel.MasterBrightness = (uint)Math.Clamp(newBright, 0, 100);
                e.Handled = true;
            };
        }

        private System.Drawing.Point? _lastAnchorPoint;

        public void SetAnchorPoint(System.Drawing.Point pt)
        {
            _lastAnchorPoint = pt;
        }

        public void ShowNearTray(System.Drawing.Point? anchorPoint = null)
        {
            if (anchorPoint.HasValue && anchorPoint.Value != System.Drawing.Point.Empty)
            {
                _lastAnchorPoint = anchorPoint;
            }

            UpdatePosition(_lastAnchorPoint);
            UpdateDwmTheme();
            Show();
            Activate();
            Focus();

            // Perform secondary pass once visual tree and font styles are realized on target monitor
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (IsVisible)
                {
                    UpdatePosition(_lastAnchorPoint);
                }
            });
        }

        private void UpdateDwmTheme()
        {
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    int darkMode = _viewModel.IsLightTheme ? 0 : 1;
                    NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                }
            }
            catch { }
        }

        public void ToggleVisibility(System.Drawing.Point? anchorPoint = null)
        {
            if (IsVisible)
            {
                Hide();
                _viewModel.CancelLockCountdown();
                _viewModel.CancelAllInputSwitches();
                MainWindow.TrimMemory();
            }
            else
            {
                ShowNearTray(anchorPoint);
            }
        }

        private static bool IsValidRectOnAnyScreen(NativeMethods.Rect rect)
        {
            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return false;
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width > 300 || height > 300 || width < 4 || height < 4) return false;

            int centerX = (rect.Left + rect.Right) / 2;
            int centerY = (rect.Top + rect.Bottom) / 2;
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                if (screen.Bounds.Contains(centerX, centerY))
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdatePosition(System.Drawing.Point? fallbackPoint = null)
        {
            try
            {
                // 1. Determine anchor target: prefer actual tray icon bounding rect from Shell
                NativeMethods.Rect iconRect = default;
                bool hasValidIconRect = _scrollHook.TryGetTrayIconRect(out iconRect) && IsValidRectOnAnyScreen(iconRect);

                System.Drawing.Point targetPoint;
                if (hasValidIconRect)
                {
                    targetPoint = new System.Drawing.Point(
                        (iconRect.Left + iconRect.Right) / 2,
                        (iconRect.Top + iconRect.Bottom) / 2);
                }
                else if (fallbackPoint.HasValue && fallbackPoint.Value != System.Drawing.Point.Empty)
                {
                    targetPoint = fallbackPoint.Value;
                }
                else if (_scrollHook.LastHoverPosition.HasValue)
                {
                    targetPoint = _scrollHook.LastHoverPosition.Value;
                }
                else
                {
                    NativeMethods.GetCursorPos(out var curPos);
                    targetPoint = new System.Drawing.Point(curPos.x, curPos.y);
                }

                // 2. Identify target Screen containing the icon/anchor
                var targetScreen = System.Windows.Forms.Screen.FromPoint(targetPoint)
                                   ?? System.Windows.Forms.Screen.PrimaryScreen
                                   ?? (System.Windows.Forms.Screen.AllScreens.Length > 0 ? System.Windows.Forms.Screen.AllScreens[0] : null);

                if (targetScreen == null) return;

                var screenBounds = targetScreen.Bounds;
                var workArea = targetScreen.WorkingArea;

                // 3. Obtain monitor DPI scale
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                try
                {
                    var pt = new NativeMethods.POINT { x = targetPoint.X, y = targetPoint.Y };
                    IntPtr hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
                    if (hMonitor != IntPtr.Zero && NativeMethods.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY) == 0 && dpiX > 0 && dpiY > 0)
                    {
                        dpiScaleX = dpiX / 96.0;
                        dpiScaleY = dpiY / 96.0;
                    }
                    else
                    {
                        var dpi = VisualTreeHelper.GetDpi(this);
                        dpiScaleX = dpi.DpiScaleX;
                        dpiScaleY = dpi.DpiScaleY;
                    }
                }
                catch
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    dpiScaleX = dpi.DpiScaleX;
                    dpiScaleY = dpi.DpiScaleY;
                }

                if (dpiScaleX <= 0) dpiScaleX = 1.0;
                if (dpiScaleY <= 0) dpiScaleY = 1.0;

                // 4. Measure layout size in DIPs and enforce MaxHeight
                double maxAllowedDipHeight = Math.Max(240, (workArea.Height / dpiScaleY) - 20);
                MaxHeight = maxAllowedDipHeight;

                Measure(new System.Windows.Size(Width, maxAllowedDipHeight));
                double windowWidth = ActualWidth > 0 ? ActualWidth : Width;
                double windowHeight = DesiredSize.Height > 0 ? DesiredSize.Height : (ActualHeight > 0 ? ActualHeight : 460);
                windowHeight = Math.Min(windowHeight, maxAllowedDipHeight);

                double physWidth = windowWidth * dpiScaleX;
                double physHeight = windowHeight * dpiScaleY;

                // 5. Determine Taskbar edge on this monitor
                bool taskbarAtBottom = workArea.Bottom < screenBounds.Bottom;
                bool taskbarAtTop = workArea.Top > screenBounds.Top;
                bool taskbarAtRight = workArea.Right < screenBounds.Right;
                bool taskbarAtLeft = workArea.Left > screenBounds.Left;

                double physLeft;
                double physTop;
                int marginX = (int)Math.Round(12 * dpiScaleX);
                int marginY = (int)Math.Round(8 * dpiScaleY);

                if (taskbarAtBottom)
                {
                    // Taskbar is at the bottom: place flyout directly above taskbar
                    double anchorTop = hasValidIconRect ? Math.Min(workArea.Bottom, iconRect.Top) : workArea.Bottom;
                    physTop = anchorTop - physHeight - marginY;
                    physLeft = targetPoint.X - (physWidth / 2.0);
                }
                else if (taskbarAtTop)
                {
                    // Taskbar is at the top: place flyout directly below taskbar
                    double anchorBottom = hasValidIconRect ? Math.Max(workArea.Top, iconRect.Bottom) : workArea.Top;
                    physTop = anchorBottom + marginY;
                    physLeft = targetPoint.X - (physWidth / 2.0);
                }
                else if (taskbarAtRight)
                {
                    // Taskbar is on the right: place flyout to the left of taskbar
                    double anchorLeft = hasValidIconRect ? Math.Min(workArea.Right, iconRect.Left) : workArea.Right;
                    physLeft = anchorLeft - physWidth - marginX;
                    physTop = targetPoint.Y - (physHeight / 2.0);
                }
                else if (taskbarAtLeft)
                {
                    // Taskbar is on the left: place flyout to the right of taskbar
                    double anchorRight = hasValidIconRect ? Math.Max(workArea.Left, iconRect.Right) : workArea.Left;
                    physLeft = anchorRight + marginX;
                    physTop = targetPoint.Y - (physHeight / 2.0);
                }
                else
                {
                    // Auto-hidden taskbar or no taskbar on this monitor
                    if (targetPoint.Y > workArea.Top + workArea.Height / 2)
                    {
                        physTop = workArea.Bottom - physHeight - marginY;
                    }
                    else
                    {
                        physTop = workArea.Top + marginY;
                    }
                    physLeft = targetPoint.X - (physWidth / 2.0);
                }

                // 6. Firmly clamp within target screen's WorkArea to guarantee 100% full visibility
                if (physLeft + physWidth > workArea.Right - marginX)
                {
                    physLeft = workArea.Right - physWidth - marginX;
                }
                if (physLeft < workArea.Left + marginX)
                {
                    physLeft = workArea.Left + marginX;
                }
                if (physTop + physHeight > workArea.Bottom - marginY)
                {
                    physTop = workArea.Bottom - physHeight - marginY;
                }
                if (physTop < workArea.Top + marginY)
                {
                    physTop = workArea.Top + marginY;
                }

                // 7. Apply to WPF and Win32
                Left = physLeft / dpiScaleX;
                Top = physTop / dpiScaleY;

                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(
                        helper.Handle,
                        NativeMethods.HWND_TOPMOST,
                        (int)Math.Round(physLeft),
                        (int)Math.Round(physTop),
                        (int)Math.Round(physWidth),
                        (int)Math.Round(physHeight),
                        NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                }

                App.Log($"[TrayFlyout] Positioned on {targetScreen.DeviceName}: Left={Left:F1}, Top={Top:F1}, W={windowWidth:F1}, H={windowHeight:F1}, dpi={dpiScaleX:F2}");
            }
            catch (Exception ex)
            {
                App.Log($"[TrayFlyout] UpdatePosition error: {ex.Message}");
            }
        }

        private void OnRefreshDisplaysClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshMonitors(forceRecreate: true);
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

        private void OnToggleMonitorMuteClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is Lumina.Models.MonitorInfo monitor)
            {
                monitor.ToggleMute();
            }
        }

        private void OnCancelInputSwitchClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is Lumina.Models.MonitorInfo monitor)
            {
                monitor.CancelInputSwitch();
            }
            else
            {
                _viewModel.CancelAllInputSwitches();
            }
        }
    }
}
