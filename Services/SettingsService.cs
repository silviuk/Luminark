using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Lumina.Models;

namespace Lumina.Services
{
    public class SettingsService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "Luminark";
        private const string LegacyRegistryName = "LuminaApp";
        private readonly string _settingsFilePath;
        private readonly object _gate = new();
        private readonly bool _integrateStartup;
        private AppSettings? _current;
        private bool? _startupState;
        internal string? LastSaveError { get; private set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

        public static bool IsPackaged()
        {
            try
            {
                int length = 0;
                int result = GetCurrentPackageFullName(ref length, null);
                return result != 15700; // APPMODEL_ERROR_NO_PACKAGE = 15700
            }
            catch
            {
                return false;
            }
        }

        public SettingsService() : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Luminark", "settings.json"), true)
        { }

        internal SettingsService(string path, bool integrateStartup = false)
        {
            _settingsFilePath = Path.GetFullPath(path);
            _integrateStartup = integrateStartup;
        }

        public AppSettings Load()
        {
            lock (_gate)
            {
                if (_current != null) return _current;
                _current = LoadCore();
                _current.Normalize();
                _startupState = _current.StartWithWindows;
                return _current;
            }
        }

        private AppSettings? ReadSettings(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                // Reject unexpectedly large local configurations before allocating JSON text.
                if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("Settings exceed 1 MiB.");
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                App.Log($"[Settings] Cannot read {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }
        private AppSettings LoadCore()
        {
            AppSettings settings = new AppSettings();
            try
            {
                var loaded = ReadSettings(_settingsFilePath) ?? ReadSettings(_settingsFilePath + ".bak");
                if (loaded != null)
                {
                    settings = loaded;
                }
                else
                {
                    // Fresh installation: initialize schedule from Windows location-based sunrise/sunset if available
                    try
                    {
                        using var nl = new NightLightService(watchChanges: false);
                        var info = nl.GetNightLightInfo();
                        if (info.Sunrise.HasValue) settings.DayTime = info.Sunrise.Value;
                        if (info.Sunset.HasValue) settings.NightTime = info.Sunset.Value;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                App.Log($"[Settings] Failed to load settings: {ex.Message}");
            }

            if (_integrateStartup) CleanLegacyStartup();

            // Synchronize StartWithWindows state with native provider if packaged
            if (_integrateStartup && IsPackaged())
            {
                try
                {
                    var task = global::Windows.ApplicationModel.StartupTask.GetAsync("LuminarkStartup").AsTask().GetAwaiter().GetResult();
                    settings.StartWithWindows = (task.State == global::Windows.ApplicationModel.StartupTaskState.Enabled ||
                                                 task.State == global::Windows.ApplicationModel.StartupTaskState.EnabledByPolicy);
                }
                catch (Exception ex)
                {
                    App.Log($"[Settings] StartupTask query error: {ex.Message}");
                }
            }

            return settings;
        }

        public bool Save(AppSettings settings)
        {
            lock (_gate)
            {
                string temporary = _settingsFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    LastSaveError = null;
                    settings.Normalize();
                    Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        JsonSerializer.Serialize(stream, settings, new JsonSerializerOptions { WriteIndented = true });
                        stream.Flush(flushToDisk: true);
                    }
                    if (File.Exists(_settingsFilePath))
                        File.Replace(temporary, _settingsFilePath, _settingsFilePath + ".bak");
                    else
                        File.Move(temporary, _settingsFilePath);
                    _current = settings;
                    if (_integrateStartup && _startupState != settings.StartWithWindows)
                    {
                        SetStartup(settings.StartWithWindows);
                        _startupState = settings.StartWithWindows;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    LastSaveError = ex.Message;
                    App.Log($"[Settings] Failed to save settings: {ex.Message}");
                    return false;
                }
                finally
                {
                    try { File.Delete(temporary); } catch (IOException) { }
                }
            }
        }
        private void CleanStartupFolderShortcut()
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (!string.IsNullOrEmpty(startupFolder) && Directory.Exists(startupFolder))
                {
                    string lnkPath = Path.Combine(startupFolder, "Luminark.lnk");
                    if (File.Exists(lnkPath))
                    {
                        File.Delete(lnkPath);
                        App.Log("[Settings] Removed redundant Startup folder shortcut: " + lnkPath);
                    }

                    string legacyLnk = Path.Combine(startupFolder, "Lumina.lnk");
                    if (File.Exists(legacyLnk))
                    {
                        File.Delete(legacyLnk);
                        App.Log("[Settings] Removed redundant legacy Startup folder shortcut: " + legacyLnk);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"[Settings] CleanStartupFolderShortcut warning: {ex.Message}");
            }
        }

        private void CleanLegacyStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key != null && key.GetValue(LegacyRegistryName) != null)
                {
                    key.DeleteValue(LegacyRegistryName, false);
                    App.Log("[Settings] Cleaned up legacy 'LuminaApp' registry entry.");
                }
            }
            catch { }

            CleanStartupFolderShortcut();
        }

        private void SetStartup(bool enable)
        {
            if (_integrateStartup) CleanLegacyStartup();

            if (_integrateStartup && IsPackaged())
            {
                try
                {
                    var task = global::Windows.ApplicationModel.StartupTask.GetAsync("LuminarkStartup").AsTask().GetAwaiter().GetResult();
                    if (enable)
                    {
                        if (task.State == global::Windows.ApplicationModel.StartupTaskState.Disabled)
                        {
                            var state = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                            App.Log($"[Startup] Packaged StartupTask RequestEnableAsync result: {state}");
                        }
                        else
                        {
                            App.Log($"[Startup] Packaged StartupTask state: {task.State}");
                        }
                    }
                    else
                    {
                        task.Disable();
                        App.Log("[Startup] Packaged StartupTask disabled.");
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[Startup] Packaged StartupTask update error: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                    if (key != null)
                    {
                        if (enable)
                        {
                            string? exePath = Environment.ProcessPath;
                            if (!string.IsNullOrEmpty(exePath))
                            {
                                key.SetValue(AppRegistryName, $"\"{exePath}\" --minimized");
                                App.Log($"[Startup] Registered unpackaged startup: {exePath}");
                            }
                        }
                        else
                        {
                            if (key.GetValue(AppRegistryName) != null)
                            {
                                key.DeleteValue(AppRegistryName, false);
                                App.Log("[Startup] Removed unpackaged startup registry entry.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[Startup] Unpackaged startup registry error: {ex.Message}");
                }
            }
        }
    }
}
