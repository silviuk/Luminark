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
        private bool _isExplicitExit = false;

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

        private void InitTrayIcon()
        {
            try
            {
                App.Log("[MainWindow] Creating System.Windows.Forms.NotifyIcon");
                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = "Lumina - Display & Theme Manager",
                    Icon = IconHelper.CreateAppIcon(),
                    Visible = true
                };

                _notifyIcon.DoubleClick += (s, e) => ShowAndActivate();

                var contextMenu = new ContextMenuStrip();
                var openItem = new ToolStripMenuItem("Open Lumina", null, (s, e) => ShowAndActivate())
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

                contextMenu.Items.Add(new ToolStripSeparator());

                var exitItem = new ToolStripMenuItem("Exit Lumina", null, (s, e) =>
                {
                    App.Log("[MainWindow] Exit clicked from tray context menu");
                    _isExplicitExit = true;
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                    }
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
            }
            catch (Exception ex)
            {
                App.Log($"[MainWindow] HwndSource hook error: {ex.Message}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == App.WM_SHOWLUMINA)
            {
                App.Log("[MainWindow] Received WM_SHOWLUMINA broadcast! Bringing window to foreground.");
                ShowAndActivate();
                handled = true;
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
            App.Log($"[MainWindow] OnClosing called (explicitExit={_isExplicitExit}, minimizeToTray={_viewModel.MinimizeToTray})");
            if (!_isExplicitExit && _viewModel.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                try
                {
                    _notifyIcon?.ShowBalloonTip(3000, "Lumina", "Lumina is running in the system tray. Click this icon anytime to open.", ToolTipIcon.Info);
                }
                catch { }
            }
            else
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                base.OnClosing(e);
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