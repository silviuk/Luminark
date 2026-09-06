using System;
using System.IO;
using System.Threading;
using System.Windows;
using Lumina.Services;
using Lumina.ViewModels;
using Wpf.Ui.Appearance;

namespace Lumina
{
    public partial class App : System.Windows.Application
    {
        private ThemeService? _themeService;
        private MonitorService? _monitorService;
        private NightLightService? _nightLightService;
        private SettingsService? _settingsService;
        private ScheduleService? _scheduleService;
        private MainViewModel? _viewModel;

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lumina", "lumina.log");

        public static void Log(string message)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        public static readonly int WM_SHOWLUMINA = NativeMethods.RegisterWindowMessage("LUMINA_ACTIVATE_WINDOW_MSG");

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Log("=== Application Starting ===");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += (s, args) =>
            {
                Log($"[DISPATCHER ERROR] {args.Exception}");
                System.Windows.MessageBox.Show(args.Exception.Message, "Lumina Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = false;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Log($"[CRITICAL DOMAIN ERROR] {args.ExceptionObject}");
            };

            const string ActivateEventName = "Lumina_SingleInstance_Activate_Event";
            if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var existingEvent))
            {
                Log("Another instance of Lumina is already running. Signaling activation event.");
                existingEvent.Set();
                existingEvent.Dispose();
                Shutdown();
                return;
            }

            var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            ThreadPool.RegisterWaitForSingleObject(activateEvent, (state, timedOut) =>
            {
                Dispatcher.Invoke(() =>
                {
                    Log("[App] Received activation signal from another instance!");
                    if (MainWindow is MainWindow mw)
                    {
                        mw.ShowAndActivate();
                    }
                });
            }, null, -1, false);

            try
            {
                Log("Initializing SettingsService");
                _settingsService = new SettingsService();
                var settings = _settingsService.Load();

                Log("Initializing ThemeService");
                _themeService = new ThemeService();

                Log("Initializing MonitorService");
                _monitorService = new MonitorService();

                Log("Initializing NightLightService");
                _nightLightService = new NightLightService();

                Log("Initializing ScheduleService");
                _scheduleService = new ScheduleService(_themeService, _monitorService, _nightLightService, () => settings);

                Log("Applying ApplicationThemeManager");
                try
                {
                    ApplicationThemeManager.Apply(_themeService.IsLightTheme() ? ApplicationTheme.Light : ApplicationTheme.Dark);
                }
                catch (Exception ex)
                {
                    Log($"ThemeManager warning: {ex.Message}");
                }

                Log("Initializing MainViewModel");
                _viewModel = new MainViewModel(_themeService, _monitorService, _nightLightService, _settingsService, _scheduleService);

                Log("Creating MainWindow");
                var mainWindow = new MainWindow(_viewModel);
                MainWindow = mainWindow;

                bool startMinimized = settings.StartMinimized || (e.Args.Length > 0 && e.Args[0] == "--minimized");
                Log($"Showing MainWindow (startMinimized={startMinimized})");

                if (!startMinimized)
                {
                    mainWindow.Show();
                    mainWindow.Visibility = Visibility.Visible;
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                    mainWindow.Topmost = true;
                    mainWindow.Topmost = false;
                    mainWindow.Focus();
                }
                else
                {
                    new System.Windows.Interop.WindowInteropHelper(mainWindow).EnsureHandle();
                    mainWindow.WindowState = WindowState.Minimized;
                    mainWindow.Hide();
                    Lumina.MainWindow.TrimMemory();
                }

                Log("Starting ScheduleService");
                _scheduleService.Start();

                Log("Application_Startup completed successfully.");

                System.Threading.Tasks.Task.Delay(3500).ContinueWith(_ =>
                {
                    Lumina.MainWindow.TrimMemory();
                });
            }
            catch (Exception ex)
            {
                Log($"[FATAL IN STARTUP] {ex}");
                System.Windows.MessageBox.Show($"Lumina failed to start:\n\n{ex}", "Lumina Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Log("Application_Exit triggered.");
            try { _scheduleService?.Stop(); } catch { }
            try { _scheduleService?.Dispose(); } catch { }
            try { _monitorService?.Dispose(); } catch { }
        }
    }
}
