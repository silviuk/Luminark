using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Microsoft.Win32;
using Lumina.Models;

namespace Lumina.Services
{
    public class ScheduleService : IDisposable
    {
        private readonly ThemeService _themeService;
        private readonly MonitorService _monitorService;
        private readonly NightLightService _nightLightService;
        private readonly Func<AppSettings> _getSettings;
        private readonly DispatcherTimer _timer;
        private bool? _lastAppliedDayMode = null;
        private Func<IEnumerable<MonitorInfo>>? _activeMonitorsProvider;

        public event Action<bool>? ScheduleTriggered;

        public ScheduleService(
            ThemeService themeService,
            MonitorService monitorService,
            NightLightService nightLightService,
            Func<AppSettings> getSettings)
        {
            _themeService = themeService;
            _monitorService = monitorService;
            _nightLightService = nightLightService;
            _getSettings = getSettings;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _timer.Tick += OnTimerTick;

            _nightLightService.StateChanged += OnNightLightStateChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
        }

        private void OnNightLightStateChanged(bool isActive)
        {
            App.Log($"[ScheduleService] NightLightService.StateChanged received (isActive={isActive}) -> Evaluating immediately");
            EvaluateSchedule(force: true);
        }

        public void SetActiveMonitorsProvider(Func<IEnumerable<MonitorInfo>> provider)
        {
            _activeMonitorsProvider = provider;
        }

        public void Start()
        {
            App.Log("[ScheduleService] Started");
            _timer.Start();
            EvaluateSchedule(force: true);
        }

        public void Stop()
        {
            App.Log("[ScheduleService] Stopped");
            _timer.Stop();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            EvaluateSchedule(force: false);
        }

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                App.Log("[ScheduleService] Power resume event");
                EvaluateSchedule(force: true);
            }
        }

        private void OnTimeChanged(object? sender, EventArgs e)
        {
            App.Log("[ScheduleService] System clock changed");
            EvaluateSchedule(force: true);
        }

        public void EvaluateSchedule(bool force = false)
        {
            try
            {
                var settings = _getSettings();
                if (!settings.AutoThemeEnabled && !settings.AutoBrightnessEnabled && !settings.SyncWithNightLight)
                {
                    return;
                }

                var now = DateTime.Now.TimeOfDay;
                bool isDay;

                if (settings.SyncWithNightLight)
                {
                    if (settings.NightLightSyncMode == 1)
                    {
                        // Follow Windows Sunset and Sunrise times
                        var info = _nightLightService.GetNightLightInfo();
                        var dayStart = info.Sunrise ?? settings.DayTime;
                        var nightStart = info.Sunset ?? settings.NightTime;
                        isDay = IsDaytime(now, dayStart, nightStart);
                    }
                    else
                    {
                        // Follow Windows Night Light live state (default)
                        bool isNightLightActive = _nightLightService.IsNightLightActive();
                        isDay = !isNightLightActive;
                    }
                }
                else
                {
                    isDay = IsDaytime(now, settings.DayTime, settings.NightTime);
                }

                if (force || _lastAppliedDayMode != isDay)
                {
                    App.Log($"[ScheduleService] Mode changed -> isDay={isDay} (force={force})");
                    _lastAppliedDayMode = isDay;

                    if (settings.AutoThemeEnabled || settings.SyncWithNightLight)
                    {
                        bool isCurrentlyLight = _themeService.IsLightTheme();
                        if (isCurrentlyLight != isDay)
                        {
                            App.Log($"[ScheduleService] Setting theme: isLight={isDay}");
                            _themeService.SetTheme(isDay);
                        }
                    }

                    if (settings.AutoBrightnessEnabled)
                    {
                        uint targetBrightness = isDay ? settings.DayBrightness : settings.NightBrightness;
                        var monitors = _activeMonitorsProvider?.Invoke() ?? _monitorService.EnumerateMonitors();
                        _monitorService.SetAllBrightness(monitors, targetBrightness);
                    }

                    ScheduleTriggered?.Invoke(isDay);
                }
            }
            catch (Exception ex)
            {
                App.Log($"[ScheduleService ERROR] {ex}");
            }
        }

        public static bool IsDaytime(TimeSpan current, TimeSpan dayStart, TimeSpan nightStart)
        {
            if (dayStart < nightStart)
            {
                return current >= dayStart && current < nightStart;
            }
            else
            {
                return current >= dayStart || current < nightStart;
            }
        }

        public void ResetLastAppliedState()
        {
            _lastAppliedDayMode = null;
        }

        public void Dispose()
        {
            _timer.Stop();
            _nightLightService.StateChanged -= OnNightLightStateChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
        }
    }
}
