using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace Lumina.Services;

public class ThemeService : IDisposable
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly Func<bool> _readAppsTheme;
    private readonly Func<bool> _readSystemTheme;
    private readonly Action<bool> _writeTheme;
    private readonly Action<bool> _applyAppTheme;
    private readonly ThemeRefreshQueue _refreshQueue;
    private readonly UserPreferenceChangedEventHandler _userPrefChangedHandler;
    private readonly object _writeGate = new();
    private (bool Apps, bool System) _lastNotified;
    private int _uiRefreshQueued;
    private int _disposed;
    public event Action<bool>? ThemeChanged;

    public ThemeService() : this(
        () => ReadTheme("AppsUseLightTheme"), () => ReadTheme("SystemUsesLightTheme"),
        WriteTheme, WindowsThemeNotifier.Refresh,
        isLight => ApplicationThemeManager.Apply(isLight ? ApplicationTheme.Light : ApplicationTheme.Dark,
            Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: true), subscribe: true) { }

    internal ThemeService(Func<bool> readAppsTheme, Func<bool> readSystemTheme, Action<bool> writeTheme,
        Action refreshShell, Action<bool> applyAppTheme, bool subscribe = false)
    {
        _readAppsTheme = readAppsTheme;
        _readSystemTheme = readSystemTheme;
        _writeTheme = writeTheme;
        _applyAppTheme = applyAppTheme;
        _refreshQueue = new ThemeRefreshQueue(refreshShell);
        _lastNotified = (IsLightTheme(), IsSystemLightTheme());
        _userPrefChangedHandler = (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Window)
                RefreshFromSystem();
        };
        if (subscribe) { try { SystemEvents.UserPreferenceChanged += _userPrefChangedHandler; } catch { } }
    }

    public bool IsLightTheme() => _readAppsTheme();
    public bool IsSystemLightTheme() => _readSystemTheme();

    public void SetTheme(bool isLight)
    {
        try
        {
            lock (_writeGate)
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                _writeTheme(isLight);
                _refreshQueue.Request();
            }
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher?.CheckAccess() == true) UpdateApplicationTheme();
            else RefreshFromSystem();
        }
        catch (Exception ex) { App.Log($"[ThemeService] Failed to set theme: {ex.Message}"); }
    }

    internal void RefreshFromSystem()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (Volatile.Read(ref _disposed) != 0 || dispatcher == null || dispatcher.HasShutdownStarted) return;
        if (Interlocked.Exchange(ref _uiRefreshQueued, 1) != 0) return;
        try { dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _uiRefreshQueued, 0);
            UpdateApplicationTheme();
        }); }
        catch (InvalidOperationException) { Interlocked.Exchange(ref _uiRefreshQueued, 0); }
    }

    private void UpdateApplicationTheme()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        // A newer toggle may have completed while SystemEvents waited in the UI
        // queue. Read the current state here, rather than replay a captured theme.
        (bool Apps, bool System) current;
        lock (_writeGate) current = (IsLightTheme(), IsSystemLightTheme());
        try { _applyAppTheme(current.Apps); } catch { }
        if (current == _lastNotified) return;
        _lastNotified = current;
        ThemeChanged?.Invoke(current.Apps);
    }

    private static bool ReadTheme(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            return key?.GetValue(name) is int value && value == 1;
        }
        catch { return false; }
    }

    private static void WriteTheme(bool isLight)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PersonalizeKeyPath);
        key.SetValue("SystemUsesLightTheme", isLight ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("AppsUseLightTheme", isLight ? 1 : 0, RegistryValueKind.DWord);
    }

    public void Dispose()
    {
        lock (_writeGate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _refreshQueue.Dispose();
        }
        try { SystemEvents.UserPreferenceChanged -= _userPrefChangedHandler; } catch { }
    }
}
