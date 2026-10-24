using System;
using System.Collections.Generic;

namespace Lumina.Models
{
    public class AppSettings
    {
        public bool AutoThemeEnabled { get; set; } = true;
        public TimeSpan DayTime { get; set; } = new TimeSpan(7, 0, 0);   // 07:00 AM
        public TimeSpan NightTime { get; set; } = new TimeSpan(20, 0, 0); // 08:00 PM

        public bool AutoBrightnessEnabled { get; set; } = false;
        public uint DayBrightness { get; set; } = 80;
        public uint NightBrightness { get; set; } = 30;

        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool LinkBrightnessToTheme { get; set; } = true;

        public bool SyncWithNightLight { get; set; } = false;
        public int NightLightSyncMode { get; set; } = 0; // 0 = Follow State, 1 = Follow Sunset/Sunrise

        public bool IsMonitorsLinked { get; set; } = true;
        public int LockScreenDelaySeconds { get; set; } = 3; // 0 = Instant, 3s, 5s, 10s
        public int MonitorInputSwitchDelaySeconds { get; set; } = 3; // 0 = Instant, 1s, 3s, 5s
        public Dictionary<string, string> CustomMonitorNames { get; set; } = new();
        public bool PreventSleep { get; set; } = false;
        public int PreventSleepDurationMinutes { get; set; } = 0; // 0 = Forever, 30, 60, 120, 240, 480
        public int TrayDoubleClickAction { get; set; } = 0; // 0 = Toggle Dark/Light Mode, 1 = Open Main Window
        public bool EnableTrayScrollBrightness { get; set; } = true;
        public bool EnableGlobalShortcuts { get; set; } = true;
        public string DarkModeShortcut { get; set; } = "Ctrl+Alt+D";
        public string LightModeShortcut { get; set; } = "Ctrl+Alt+L";
        public string ToggleThemeShortcut { get; set; } = "Ctrl+Alt+T";
        public string LockAndTurnOffShortcut { get; set; } = "Win+J";
        public string BrightnessUpShortcut { get; set; } = "Ctrl+Alt+Up";
        public string BrightnessDownShortcut { get; set; } = "Ctrl+Alt+Down";
        public int BrightnessStep { get; set; } = 5; // 1, 2, 5, 10
        public bool SyncLaptopBrightnessKeys { get; set; } = true;
        public bool EnableBrightnessOsd { get; set; } = true;

        // Key: Monitor ID, Value: Dictionary of Input Code -> Custom Name
        public Dictionary<string, Dictionary<uint, string>> CustomInputNames { get; set; } = new();

        // Key: Monitor ID, Value: List of Input Codes that should appear in the flyout dropdown
        public Dictionary<string, List<uint>> VisibleInputCodes { get; set; } = new();

        // Known external monitors for multi-host retention when display switches to another device
        public List<string> KnownExternalMonitorIds { get; set; } = new();

        public void Normalize()
        {
            DayTime = ValidTime(DayTime, TimeSpan.FromHours(7));
            NightTime = ValidTime(NightTime, TimeSpan.FromHours(20));
            DayBrightness = Math.Min(DayBrightness, 100);
            NightBrightness = Math.Min(NightBrightness, 100);
            BrightnessStep = new[] { 1, 2, 5, 10 }.Contains(BrightnessStep) ? BrightnessStep : 5;
            NightLightSyncMode = NightLightSyncMode == 1 ? 1 : 0;
            TrayDoubleClickAction = TrayDoubleClickAction == 1 ? 1 : 0;
            LockScreenDelaySeconds = Math.Clamp(LockScreenDelaySeconds, 0, 60);
            MonitorInputSwitchDelaySeconds = Math.Clamp(MonitorInputSwitchDelaySeconds, 0, 60);
            PreventSleepDurationMinutes = Math.Clamp(PreventSleepDurationMinutes, 0, 10080);
            CustomMonitorNames ??= new();
            CustomInputNames ??= new();
            VisibleInputCodes ??= new();
            KnownExternalMonitorIds ??= new();
            foreach (var key in CustomMonitorNames.Keys.ToArray())
                if (string.IsNullOrWhiteSpace(CustomMonitorNames[key])) CustomMonitorNames.Remove(key);
            foreach (var key in CustomInputNames.Keys.ToArray())
            {
                CustomInputNames[key] ??= new();
                foreach (var code in CustomInputNames[key].Keys.ToArray())
                    if (code == 0 || code > ushort.MaxValue || string.IsNullOrWhiteSpace(CustomInputNames[key][code]))
                        CustomInputNames[key].Remove(code);
            }
            foreach (var key in VisibleInputCodes.Keys.ToArray())
                VisibleInputCodes[key] = (VisibleInputCodes[key] ?? new()).Where(c => c > 0 && c <= ushort.MaxValue).Distinct().ToList();
            KnownExternalMonitorIds = KnownExternalMonitorIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            DarkModeShortcut ??= "Ctrl+Alt+D";
            LightModeShortcut ??= "Ctrl+Alt+L";
            ToggleThemeShortcut ??= "Ctrl+Alt+T";
            LockAndTurnOffShortcut ??= "Win+J";
            BrightnessUpShortcut ??= "Ctrl+Alt+Up";
            BrightnessDownShortcut ??= "Ctrl+Alt+Down";
        }

        private static TimeSpan ValidTime(TimeSpan value, TimeSpan fallback) =>
            value >= TimeSpan.Zero && value < TimeSpan.FromDays(1) ? value : fallback;
    }
}
