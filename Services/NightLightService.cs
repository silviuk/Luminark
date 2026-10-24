using System;
using Microsoft.Win32;

namespace Lumina.Services
{
    public class NightLightScheduleInfo
    {
        public bool IsSupported { get; set; }
        public bool IsActive { get; set; }
        public TimeSpan? Sunset { get; set; }
        public TimeSpan? Sunrise { get; set; }
        public TimeSpan? ScheduleStart { get; set; }
        public TimeSpan? ScheduleEnd { get; set; }
    }

    public class NightLightService : IDisposable
    {
        private const string CloudStoreRootPath = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current";
        private const string StateKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate";
        private const string SettingsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.settings\windows.data.bluelightreduction.settings";

        public event Action<bool>? StateChanged;

        private readonly RegistryChangeWatcher? _watcher;
        private bool _lastActive;
        private int _disposed;

        public NightLightService(bool watchChanges = true)
        {
            _lastActive = IsNightLightActive();
            if (watchChanges)
                _watcher = new RegistryChangeWatcher(CloudStoreRootPath, OnRegistryChanged);
        }

        private void OnRegistryChanged()
        {
            bool active = IsNightLightActive();
            if (active == _lastActive || Volatile.Read(ref _disposed) != 0) return;
            _lastActive = active;
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (Volatile.Read(ref _disposed) == 0) StateChanged?.Invoke(active);
            });
        }
        public bool IsSupported()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsTimeBetween(TimeSpan current, TimeSpan start, TimeSpan end)
        {
            if (start < end)
            {
                return current >= start && current < end;
            }
            else if (start > end)
            {
                // Overnight schedule, e.g. 21:00 to 07:00
                return current >= start || current < end;
            }
            return false;
        }

        public bool IsNightLightActive()
        {
            return GetNightLightInfo().IsActive;
        }

        public NightLightScheduleInfo GetNightLightInfo()
        {
            var info = new NightLightScheduleInfo
            {
                IsSupported = IsSupported(),
                IsActive = false
            };

            if (!info.IsSupported) return info;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
                if (key?.GetValue("Data") is byte[] data)
                {
                    // Pattern for Schedule Start: CA-14-0E-HH-MM
                    int idxStart = FindPattern(data, new byte[] { 0xCA, 0x14, 0x0E });
                    if (idxStart >= 0 && idxStart + 4 < data.Length)
                    {
                        info.ScheduleStart = new TimeSpan(data[idxStart + 3], data[idxStart + 4], 0);
                    }

                    // Pattern for Schedule End: CA-1E-0E-HH-MM
                    int idxEnd = FindPattern(data, new byte[] { 0xCA, 0x1E, 0x0E });
                    if (idxEnd >= 0 && idxEnd + 4 < data.Length)
                    {
                        info.ScheduleEnd = new TimeSpan(data[idxEnd + 3], data[idxEnd + 4], 0);
                    }

                    // Pattern for Sunset: CA-32-0E-HH-[2E]-MM
                    int idxSunset = FindPattern(data, new byte[] { 0xCA, 0x32, 0x0E });
                    if (idxSunset >= 0 && idxSunset + 4 < data.Length)
                    {
                        int hour = data[idxSunset + 3];
                        int minute = (idxSunset + 5 < data.Length && data[idxSunset + 4] == 0x2E)
                            ? data[idxSunset + 5]
                            : data[idxSunset + 4];
                        info.Sunset = new TimeSpan(hour, minute, 0);
                    }

                    // Pattern for Sunrise: CA-3C-0E-HH-[2E]-MM
                    int idxSunrise = FindPattern(data, new byte[] { 0xCA, 0x3C, 0x0E });
                    if (idxSunrise >= 0 && idxSunrise + 4 < data.Length)
                    {
                        int hour = data[idxSunrise + 3];
                        int minute = (idxSunrise + 5 < data.Length && data[idxSunrise + 4] == 0x2E)
                            ? data[idxSunrise + 5]
                            : data[idxSunrise + 4];
                        info.Sunrise = new TimeSpan(hour, minute, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading Night Light settings: {ex.Message}");
            }

            // Determine if Night Light is currently active:
            // 1. Direct state byte from bluelightreductionstate
            bool stateByteActive = false;
            try
            {
                using var stateKey = Registry.CurrentUser.OpenSubKey(StateKeyPath);
                if (stateKey?.GetValue("Data") is byte[] stateData && stateData.Length > 18)
                {
                    int idx = FindPattern(stateData, new byte[] { 0x2A, 0x2B, 0x0E });
                    byte stateByte = (idx >= 0 && idx + 3 < stateData.Length) ? stateData[idx + 3] : stateData[18];
                    stateByteActive = stateByte == 0x13 || stateByte == 0x14 || stateByte == 0x15;
                }
            }
            catch { }

            if (stateByteActive)
            {
                info.IsActive = true;
                return info;
            }

            // 2. Schedule-based activation (Windows 11 schedule does not flip the manual state byte)
            var now = DateTime.Now.TimeOfDay;
            if (info.ScheduleStart.HasValue && info.ScheduleEnd.HasValue && info.ScheduleStart.Value != info.ScheduleEnd.Value)
            {
                if (IsTimeBetween(now, info.ScheduleStart.Value, info.ScheduleEnd.Value))
                {
                    info.IsActive = true;
                    return info;
                }
            }
            else if (info.Sunset.HasValue && info.Sunrise.HasValue)
            {
                if (IsTimeBetween(now, info.Sunset.Value, info.Sunrise.Value))
                {
                    info.IsActive = true;
                    return info;
                }
            }

            info.IsActive = false;
            return info;
        }


        private static int FindPattern(byte[] source, byte[] pattern)
        {
            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _watcher?.Dispose();
        }
    }
}