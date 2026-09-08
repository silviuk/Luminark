using System;

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
        public int NightLightSyncMode { get; set; } = 0; // 0 = Follow State, 1 = Follow Sunset/Sunrise, 2 = Drive Night Light
        public bool LinkThemeToNightLight { get; set; } = false; // When toggling Light/Dark theme, also toggle Night Light (Light -> NL Off, Dark -> NL On)

        public bool IsMonitorsLinked { get; set; } = true;
        public int LockScreenDelaySeconds { get; set; } = 3; // 0 = Instant, 3s, 5s, 10s
        public bool PreventSleep { get; set; } = false;
        public int PreventSleepDurationMinutes { get; set; } = 0; // 0 = Forever, 30, 60, 120, 240, 480
    }
}
