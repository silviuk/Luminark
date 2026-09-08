using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Lumina.Services;
using Lumina.ViewModels;
using Wpf.Ui.Controls;

namespace Lumina
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private TrayFlyoutWindow? _flyoutWindow;
        private TrayScrollHook? _scrollHook;
        private System.Windows.Threading.DispatcherTimer? _clickTimer;
        private bool _isExplicitExit = false;
        private bool _isSysCommandClose = false;

        private const int WM_CLOSE = 0x0010;
        private const int WM_QUERYENDSESSION = 0x0011;
        private const int WM_ENDSESSION = 0x0016;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;

        public List<int> HoursList { get; } = Enumerable.Range(0, 24).ToList();
        public List<int> MinutesList { get; } = Enumerable.Range(0, 60).ToList();

        public MainWindow(MainViewModel viewModel)
        {
            App.Log("[MainWindow] Constructor started");
            _viewModel = viewModel;
            DataContext = _viewModel;

            App.Log("[MainWindow] Calling InitializeComponent");
            InitializeComponent();
            App.Log("[MainWindow] InitializeComponent completed");

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsLightTheme) ||
                    e.PropertyName == nameof(MainViewModel.IsSystemLightTheme))
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateDwmTheme(_viewModel.IsLightTheme);
                        UpdateTrayIcon();
                    });
                }
            };

            InitTrayIcon();

            Loaded += (s, e) =>
            {
                App.Log($"[MainWindow] Loaded -> IsVisible={IsVisible}, ActualWidth={ActualWidth}, ActualHeight={ActualHeight}");
                Activate();
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                {
                    try { Dispatcher.Invoke(TrimMemory); } catch { }
                });
            };

            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    TrimMemory();
                }
            };

            ContentRendered += (s, e) => App.Log($"[MainWindow] ContentRendered -> IsVisible={IsVisible}");
            IsVisibleChanged += (s, e) =>
            {
                App.Log($"[MainWindow] IsVisibleChanged -> {IsVisible}");
                if (!IsVisible)
                {
                    TrimMemory();
                }
            };
        }

        private Icon? _currentTrayIcon;

        private void InitTrayIcon()
        {
            try
            {
                App.Log("[MainWindow] Creating System.Windows.Forms.NotifyIcon");
                _currentTrayIcon = IconHelper.CreateDynamicTrayIcon(_viewModel.IsLightTheme, _viewModel.IsSystemLightTheme);
                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = $"Luminark ({_viewModel.MasterBrightness}%) - Display & Theme Manager",
                    Icon = _currentTrayIcon,
                    Visible = true
                };

                _scrollHook = new TrayScrollHook(_notifyIcon);
                _scrollHook.Scrolled += delta =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        int step = delta > 0 ? 5 : -5;
                        long newBright = (long)_viewModel.MasterBrightness + step;
                        _viewModel.MasterBrightness = (uint)Math.Clamp(newBright, 0, 100);
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Text = $"Luminark ({_viewModel.MasterBrightness}%) - Display & Theme Manager";
                        }
                        App.Log($"[MainWindow] Tray scroll -> MasterBrightness={_viewModel.MasterBrightness}%");
                    });
                };

                _flyoutWindow = new TrayFlyoutWindow(_viewModel, _scrollHook, () => ShowAndActivate());

                _clickTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime)
                };
                _clickTimer.Tick += (s, e) =>
                {
                    _clickTimer.Stop();
                    Dispatcher.Invoke(() => _flyoutWindow?.ToggleVisibility());
                };

                _notifyIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _clickTimer.Stop();
                        _clickTimer.Start();
                    }
                };

                _notifyIcon.DoubleClick += (s, e) =>
                {
                    _clickTimer.Stop();
                    if (_viewModel.TrayDoubleClickAction == 1)
                    {
                        App.Log("[MainWindow] Double-click on tray icon -> ShowAndActivate");
                        Dispatcher.Invoke(() => ShowAndActivate());
                    }
                    else
                    {
                        App.Log("[MainWindow] Double-click on tray icon -> ToggleTheme");
                        Dispatcher.Invoke(() => _viewModel.ToggleTheme());
                    }
                };

                var contextMenu = new ContextMenuStrip();
                var openFlyoutItem = new ToolStripMenuItem("Quick Controls", null, (s, e) => Dispatcher.Invoke(() => _flyoutWindow?.ShowNearTray()));
                contextMenu.Items.Add(openFlyoutItem);

                var openItem = new ToolStripMenuItem("Open Luminark Settings", null, (s, e) => ShowAndActivate())
                {
                    Font = new Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
                };
                contextMenu.Items.Add(openItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                var toggleThemeItem = new ToolStripMenuItem("Toggle Dark / Light Mode", null, (s, e) =>
                {
                    Dispatcher.Invoke(() => _viewModel.ToggleTheme());
                });
                contextMenu.Items.Add(toggleThemeItem);

                var brightnessMenu = new ToolStripMenuItem("Master Brightness");
                brightnessMenu.DropDownItems.Add("100%", null, (s, e) => Dispatcher.Invoke(() => _viewModel.MasterBrightness = 100));
                brightnessMenu.DropDownItems.Add("75%", null, (s, e) => Dispatcher.Invoke(() => _viewModel.MasterBrightness = 75));
                brightnessMenu.DropDownItems.Add("50%", null, (s, e) => Dispatcher.Invoke(() => _viewModel.MasterBrightness = 50));
                brightnessMenu.DropDownItems.Add("25%", null, (s, e) => Dispatcher.Invoke(() => _viewModel.MasterBrightness = 25));
                contextMenu.Items.Add(brightnessMenu);

                var lockItem = new ToolStripMenuItem("Lock & Screen Off", null, (s, e) =>
                {
                    Dispatcher.Invoke(() => _viewModel.StartLockAndTurnOff());
                });
                contextMenu.Items.Add(lockItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                var exitItem = new ToolStripMenuItem("Exit Luminark", null, (s, e) =>
                {
                    App.Log("[MainWindow] Exit clicked from tray context menu");
                    PrepareExplicitExit();
                    System.Windows.Application.Current.Shutdown();
                });
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;
                App.Log("[MainWindow] NotifyIcon created and configured successfully");
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] Tray icon warning: {ex.Message}");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                App.Log($"[MainWindow] OnSourceInitialized -> Handle={handle}");
                var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
                source?.AddHook(WndProc);
                UpdateDwmTheme(_viewModel.IsLightTheme);
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] HwndSource hook error: {ex.Message}");
            }
        }

        public void UpdateDwmTheme(bool isLight)
        {
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    int darkMode = isLight ? 0 : 1;
                    NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                    App.Log($"[MainWindow] Updated DWM theme: darkMode={darkMode}");
                }
                SetResourceReference(Window.BackgroundProperty, "ApplicationBackgroundBrush");
                SetResourceReference(Window.ForegroundProperty, "TextFillColorPrimaryBrush");
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] DwmSetWindowAttribute error: {ex.Message}");
            }
        }

        public void UpdateTrayIcon()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    var oldIcon = _currentTrayIcon;
                    _currentTrayIcon = IconHelper.CreateDynamicTrayIcon(_viewModel.IsLightTheme, _viewModel.IsSystemLightTheme);
                    _notifyIcon.Icon = _currentTrayIcon;
                    if (oldIcon != null)
                    {
                        NativeMethods.DestroyIcon(oldIcon.Handle);
                        oldIcon.Dispose();
                    }
                    App.Log($"[MainWindow] Updated tray icon: isLightMode={_viewModel.IsLightTheme}, isTaskbarLight={_viewModel.IsSystemLightTheme}");
                }
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] UpdateTrayIcon error: {ex.Message}");
            }
        }

        public void PrepareExplicitExit()
        {
            _isExplicitExit = true;
            try
            {
                _scrollHook?.Dispose();
                _flyoutWindow?.Close();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] PrepareExplicitExit warning: {ex.Message}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == App.WM_SHOWLUMINARK)
            {
                App.Log("[MainWindow] Received WM_SHOWLUMINARK broadcast! Bringing window to foreground.");
                ShowAndActivate();
                handled = true;
            }
            else if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_CLOSE)
            {
                _isSysCommandClose = true;
            }
            else if (msg == WM_QUERYENDSESSION)
            {
                App.Log($"[MainWindow] WM_QUERYENDSESSION received (lParam={lParam}) -> allowing shutdown");
                PrepareExplicitExit();
                handled = true;
                return (IntPtr)1; // Return TRUE to indicate willingness to shut down
            }
            else if (msg == WM_ENDSESSION)
            {
                App.Log($"[MainWindow] WM_ENDSESSION received (wParam={wParam})");
                PrepareExplicitExit();
                if (wParam != IntPtr.Zero)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                handled = true;
                return IntPtr.Zero;
            }
            else if (msg == WM_CLOSE)
            {
                App.Log($"[MainWindow] WM_CLOSE received (isSysCommandClose={_isSysCommandClose}, explicitExit={_isExplicitExit})");
                if (!_isSysCommandClose && !_isExplicitExit)
                {
                    App.Log("[MainWindow] External/RestartManager WM_CLOSE detected -> triggering explicit exit");
                    PrepareExplicitExit();
                    Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                    handled = true;
                    return IntPtr.Zero;
                }
            }
            return IntPtr.Zero;
        }

        public void ShowAndActivate()
        {
            App.Log("[MainWindow] ShowAndActivate called");
            Show();
            Visibility = Visibility.Visible;
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            App.Log($"[MainWindow] OnClosing called (explicitExit={_isExplicitExit}, isSysCommandClose={_isSysCommandClose}, minimizeToTray={_viewModel.MinimizeToTray})");
            if (!_isExplicitExit && _isSysCommandClose && _viewModel.MinimizeToTray)
            {
                e.Cancel = true;
                _isSysCommandClose = false;
                Hide();
                try
                {
                    _notifyIcon?.ShowBalloonTip(3000, "Luminark", "Luminark is running in the system tray. Click this icon anytime to open.", ToolTipIcon.Info);
                }
                catch { }
            }
            else
            {
                _isSysCommandClose = false;
                PrepareExplicitExit();
                base.OnClosing(e);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void OnThemeSwitchClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleTheme();
        }

        private void OnQuickThemeToggleClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleTheme();
        }

        private void OnRefreshDisplaysClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshMonitors();
        }

        private void OnPresetClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string tagStr && uint.TryParse(tagStr, out uint val))
            {
                _viewModel.MasterBrightness = val;
            }
        }

        private void OnImportSunsetSunriseClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.ImportSunsetSunriseTimes();
        }

        private void OnOpenGitHubClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/silviuk/Luminark")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] Failed to open GitHub: {ex.Message}");
            }
        }

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                NativeMethods.EmptyWorkingSet(proc.Handle);
                App.Log($"[MainWindow] TrimMemory completed. Current WS: {proc.WorkingSet64 / 1024 / 1024} MB");
            }
            catch { }
        }
    }
}