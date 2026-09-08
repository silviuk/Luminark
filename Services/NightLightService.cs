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

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath);
                    if (key == null)
                    {
                        System.Threading.Thread.Sleep(5000);
                        continue;
                    }

                    int result = NativeMethods.RegNotifyChangeKeyValue(
                        key.Handle.DangerousGetHandle(),
                        false,
                        NativeMethods.REG_NOTIFY_CHANGE_LAST_SET,
                        changeEvent.SafeWaitHandle.DangerousGetHandle(),
                        true);

                    if (result != 0)
                    {
                        System.Threading.Thread.Sleep(5000);
                        continue;
                    }

                    int waitIndex = System.Threading.WaitHandle.WaitAny(
                        new System.Threading.WaitHandle[] { changeEvent, _cts.Token.WaitHandle });

                    if (waitIndex == 1 || _cts.IsCancellationRequested)
                    {
                        break;
                    }

                    // Debounce slightly to let registry commit complete
                    System.Threading.Thread.Sleep(100);

                    bool currentActive = IsNightLightActive();
                    if (currentActive != lastActive)
                    {
                        lastActive = currentActive;
                        App.Log($"[NightLightService] Native event: Night Light active changed -> {currentActive}");
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

        public bool SetNightLightState(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath, writable: true);
                if (key?.GetValue("Data") is byte[] data && data.Length > 18)
                {
                    bool currentActive = data[18] == 0x13 || data[18] == 0x15 || data[18] == 0x14;
                    if (currentActive == enable) return true;

                    if (enable)
                    {
                        // Enable Night Light
                        byte[] newData = new byte[data.Length + 5];
                        Array.Copy(data, 0, newData, 0, 18);
                        newData[18] = 0x15;
                        newData[19] = 0x00;
                        newData[20] = 0x00;
                        newData[21] = 0x00;
                        newData[22] = 0x00;
                        if (data.Length > 19)
                        {
                            Array.Copy(data, 19, newData, 24, data.Length - 19);
                        }
                        key.SetValue("Data", newData, RegistryValueKind.Binary);
                    }
                    else
                    {
                        // Disable Night Light
                        data[18] = 0x10;
                        key.SetValue("Data", data, RegistryValueKind.Binary);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting Night Light state: {ex.Message}");
            }
            return false;
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
