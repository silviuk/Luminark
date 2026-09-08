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

        private readonly System.Threading.CancellationTokenSource _cts = new();
        private System.Threading.Thread? _watcherThread;

        public NightLightService()
        {
            StartWatcher();
        }

        private void StartWatcher()
        {
            try
            {
                _watcherThread = new System.Threading.Thread(WatcherLoop)
                {
                    IsBackground = true,
                    Name = "NightLightRegistryWatcher"
                };
                _watcherThread.Start();
            }
            catch (Exception ex)
            {
                App.Log($"[NightLightService] Failed to start registry watcher: {ex.Message}");
            }
        }

        private void WatcherLoop()
        {
            using var changeEvent = new System.Threading.AutoResetEvent(false);
            bool lastActive = IsNightLightActive();
            App.Log($"[NightLightService] Watcher started. Initial active state: {lastActive}");

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(CloudStoreRootPath);
                    if (key != null)
                    {
                        NativeMethods.RegNotifyChangeKeyValue(
                            key.Handle.DangerousGetHandle(),
                            true, // Watch all CloudStore bluelight subtrees
                            NativeMethods.REG_NOTIFY_CHANGE_NAME | NativeMethods.REG_NOTIFY_CHANGE_LAST_SET,
                            changeEvent.SafeWaitHandle.DangerousGetHandle(),
                            true);
                    }

                    // Wait on native registry event, cancellation token, or 1000ms safety timeout
                    int waitIndex = System.Threading.WaitHandle.WaitAny(
                        new System.Threading.WaitHandle[] { changeEvent, _cts.Token.WaitHandle },
                        1000);

                    if (waitIndex == 1 || _cts.IsCancellationRequested)
                    {
                        break;
                    }

                    if (waitIndex == 0)
                    {
                        // Native event triggered -> brief debounce for Windows commit
                        System.Threading.Thread.Sleep(80);
                    }

                    bool currentActive = IsNightLightActive();
                    if (currentActive != lastActive)
                    {
                        lastActive = currentActive;
                        App.Log($"[NightLightService] Night Light active changed -> {currentActive}");
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            StateChanged?.Invoke(currentActive);
                        });
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[NightLightService Watcher ERROR] {ex.Message}");
                    System.Threading.Thread.Sleep(2000);
                }
            }
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

        public bool IsNightLightActive()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath);
                if (key?.GetValue("Data") is byte[] data && data.Length > 18)
                {
                    // Look for tag 2A-2B-0E which directly precedes the state byte in CloudStore
                    int idx = FindPattern(data, new byte[] { 0x2A, 0x2B, 0x0E });
                    byte stateByte = (idx >= 0 && idx + 3 < data.Length) ? data[idx + 3] : data[18];

                    // Active states: 0x13, 0x14, 0x15. Inactive: 0x10, 0x11, 0x12
                    return stateByte == 0x13 || stateByte == 0x14 || stateByte == 0x15;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading Night Light state: {ex.Message}");
            }
            return false;
        }

        public NightLightScheduleInfo GetNightLightInfo()
        {
            var info = new NightLightScheduleInfo
            {
                IsSupported = IsSupported(),
                IsActive = IsNightLightActive()
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
            try
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            catch { }
        }
    }
}
