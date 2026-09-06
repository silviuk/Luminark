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

    public class NightLightService
    {
        private const string StateKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate";
        private const string SettingsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.settings\windows.data.bluelightreduction.settings";

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
                    // 0x10 = Off, 0x13 or 0x15 = On
                    return data[18] == 0x13 || data[18] == 0x15 || data[18] == 0x14;
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

                    // Pattern for Sunset: CA-32-0E-HH-MM
                    int idxSunset = FindPattern(data, new byte[] { 0xCA, 0x32, 0x0E });
                    if (idxSunset >= 0 && idxSunset + 4 < data.Length)
                    {
                        info.Sunset = new TimeSpan(data[idxSunset + 3], data[idxSunset + 4], 0);
                    }

                    // Pattern for Sunrise: CA-3C-0E-HH-MM
                    int idxSunrise = FindPattern(data, new byte[] { 0xCA, 0x3C, 0x0E });
                    if (idxSunrise >= 0 && idxSunrise + 4 < data.Length)
                    {
                        info.Sunrise = new TimeSpan(data[idxSunrise + 3], data[idxSunrise + 4], 0);
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
    }
}
