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

        public SettingsService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "Luminark");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        public AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) settings = loaded;
                }
            }
            catch (Exception ex)
            {
                App.Log($"[Settings] Failed to load settings: {ex.Message}");
            }

            CleanLegacyStartup();

            // Synchronize StartWithWindows state with native provider if packaged
            if (IsPackaged())
            {
                try
                {
                    var task = Windows.ApplicationModel.StartupTask.GetAsync("LuminarkStartup").AsTask().GetAwaiter().GetResult();
                    settings.StartWithWindows = (task.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                                                 task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy);
                }
                catch (Exception ex)
                {
                    App.Log($"[Settings] StartupTask query error: {ex.Message}");
                }
            }

            return settings;
        }

        public void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);

                // Update Windows Startup
                SetStartup(settings.StartWithWindows);
            }
            catch (Exception ex)
            {
                App.Log($"[Settings] Failed to save settings: {ex.Message}");
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
        }

        private void SetStartup(bool enable)
        {
            CleanLegacyStartup();

            if (IsPackaged())
            {
                try
                {
                    var task = Windows.ApplicationModel.StartupTask.GetAsync("LuminarkStartup").AsTask().GetAwaiter().GetResult();
                    if (enable)
                    {
                        if (task.State == Windows.ApplicationModel.StartupTaskState.Disabled)
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
