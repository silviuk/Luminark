using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Lumina.Models;
using Lumina.Services;

namespace Lumina.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ThemeService _themeService;
        private readonly MonitorService _monitorService;
        private readonly NightLightService _nightLightService;
        private readonly SettingsService _settingsService;
        private readonly ScheduleService _scheduleService;

        private AppSettings _settings;
        private bool _isUpdatingMasterBrightness = false;
        private bool _isUpdatingIndividualBrightness = false;

        public ObservableCollection<MonitorInfo> Monitors { get; } = new();

        public MainViewModel(
            ThemeService themeService,
            MonitorService monitorService,
            NightLightService nightLightService,
            SettingsService settingsService,
            ScheduleService scheduleService)
        {
            _themeService = themeService;
            _monitorService = monitorService;
            _nightLightService = nightLightService;
            _settingsService = settingsService;
            _scheduleService = scheduleService;

            _settings = _settingsService.Load();

            _themeService.ThemeChanged += OnThemeChanged;
            _scheduleService.ScheduleTriggered += OnScheduleTriggered;
            _scheduleService.SetActiveMonitorsProvider(() => Monitors);

            App.Log("MainViewModel: RefreshMonitors started");
            RefreshMonitors();
            App.Log("MainViewModel: RefreshMonitors completed");

            UpdateThemeProperties();

            App.Log("MainViewModel: RefreshNightLightInfo started");
            RefreshNightLightInfo();
            App.Log("MainViewModel: RefreshNightLightInfo completed");
        }

        public AppSettings Settings => _settings;

        #region Theme Properties

        public bool IsLightTheme => _themeService.IsLightTheme();
        public bool IsDarkMode => !IsLightTheme;

        public string ThemeStatusText => IsLightTheme ? "Light Mode Active" : "Dark Mode Active";
        public string ThemeSubText => IsLightTheme
            ? "Windows shell and supported applications are using bright theme."
            : "Windows shell and supported applications are using dark theme.";

        public void ToggleTheme()
        {
            bool newIsLight = !IsLightTheme;
            _themeService.SetTheme(newIsLight);

            if (_settings.LinkBrightnessToTheme && Monitors.Count > 0)
            {
                uint targetBrightness = newIsLight ? _settings.DayBrightness : _settings.NightBrightness;
                MasterBrightness = targetBrightness;
            }

            UpdateThemeProperties();
        }

        public void SetTheme(bool isLight)
        {
            _themeService.SetTheme(isLight);
            if (_settings.LinkBrightnessToTheme && Monitors.Count > 0)
            {
                uint targetBrightness = isLight ? _settings.DayBrightness : _settings.NightBrightness;
                MasterBrightness = targetBrightness;
            }
            UpdateThemeProperties();
        }

        private void OnThemeChanged(bool isLight)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(UpdateThemeProperties);
        }

        private void OnScheduleTriggered(bool isDay)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateThemeProperties();
                if (_settings.AutoBrightnessEnabled)
                {
                    uint target = isDay ? _settings.DayBrightness : _settings.NightBrightness;
                    MasterBrightness = target;
                }
            });
        }

        private void UpdateThemeProperties()
        {
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkMode));
            OnPropertyChanged(nameof(ThemeStatusText));
            OnPropertyChanged(nameof(ThemeSubText));
        }

        #endregion

        #region Monitor Properties

        private uint _masterBrightness = 50;
        public uint MasterBrightness
        {
            get => _masterBrightness;
            set
            {
                if (_masterBrightness != value)
                {
                    _masterBrightness = Math.Clamp(value, 0, 100);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MasterBrightnessText));

                    if (!_isUpdatingIndividualBrightness)
                    {
                        _isUpdatingMasterBrightness = true;
                        foreach (var mon in Monitors)
                        {
                            mon.CurrentBrightness = _masterBrightness;
                            _monitorService.SetBrightness(mon, _masterBrightness);
                        }
                        _isUpdatingMasterBrightness = false;
                    }
                }
            }
        }

        public string MasterBrightnessText => $"{MasterBrightness}%";

        public string ConnectedMonitorsCountText => Monitors.Count switch
        {
            0 => "No compatible displays detected",
            1 => "1 Display connected",
            _ => $"{Monitors.Count} Displays connected"
        };

        public void RefreshMonitors()
        {
            var detected = _monitorService.EnumerateMonitors();
            Monitors.Clear();
            foreach (var mon in detected)
            {
                mon.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MonitorInfo.CurrentBrightness) && !_isUpdatingMasterBrightness)
                    {
                        _isUpdatingIndividualBrightness = true;
                        _monitorService.SetBrightness(mon, mon.CurrentBrightness);
                        if (Monitors.Count > 0)
                        {
                            _masterBrightness = (uint)Math.Round(Monitors.Average(m => m.CurrentBrightness));
                            OnPropertyChanged(nameof(MasterBrightness));
                            OnPropertyChanged(nameof(MasterBrightnessText));
                        }
                        _isUpdatingIndividualBrightness = false;
                    }
                };
                Monitors.Add(mon);
            }

            if (Monitors.Count > 0)
            {
                _masterBrightness = (uint)Math.Round(Monitors.Average(m => m.CurrentBrightness));
            }
            OnPropertyChanged(nameof(MasterBrightness));
            OnPropertyChanged(nameof(MasterBrightnessText));
            OnPropertyChanged(nameof(ConnectedMonitorsCountText));
        }

        public void SetIndividualBrightness(MonitorInfo monitor, uint brightness)
        {
            _monitorService.SetBrightness(monitor, brightness);
        }

        #endregion

        #region Schedule & Settings Properties

        public bool AutoThemeEnabled
        {
            get => _settings.AutoThemeEnabled;
            set
            {
                if (_settings.AutoThemeEnabled != value)
                {
                    _settings.AutoThemeEnabled = value;
                    OnPropertyChanged();
                    SaveSettings();
                    _scheduleService.EvaluateSchedule(force: true);
                }
            }
        }

        public int DayHour
        {
            get => _settings.DayTime.Hours;
            set
            {
                if (_settings.DayTime.Hours != value)
                {
                    _settings.DayTime = new TimeSpan(Math.Clamp(value, 0, 23), _settings.DayTime.Minutes, 0);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DayTimeDisplay));
                    SaveSettings();
                    _scheduleService.ResetLastAppliedState();
                }
            }
        }

        public int DayMinute
        {
            get => _settings.DayTime.Minutes;
            set
            {
                if (_settings.DayTime.Minutes != value)
                {
                    _settings.DayTime = new TimeSpan(_settings.DayTime.Hours, Math.Clamp(value, 0, 59), 0);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DayTimeDisplay));
                    SaveSettings();
                    _scheduleService.ResetLastAppliedState();
                }
            }
        }

        public string DayTimeDisplay => $"{_settings.DayTime.Hours:D2}:{_settings.DayTime.Minutes:D2}";

        public int NightHour
        {
            get => _settings.NightTime.Hours;
            set
            {
                if (_settings.NightTime.Hours != value)
                {
                    _settings.NightTime = new TimeSpan(Math.Clamp(value, 0, 23), _settings.NightTime.Minutes, 0);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(NightTimeDisplay));
                    SaveSettings();
                    _scheduleService.ResetLastAppliedState();
                }
            }
        }

        public int NightMinute
        {
            get => _settings.NightTime.Minutes;
            set
            {
                if (_settings.NightTime.Minutes != value)
                {
                    _settings.NightTime = new TimeSpan(_settings.NightTime.Hours, Math.Clamp(value, 0, 59), 0);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(NightTimeDisplay));
                    SaveSettings();
                    _scheduleService.ResetLastAppliedState();
                }
            }
        }

        public string NightTimeDisplay => $"{_settings.NightTime.Hours:D2}:{_settings.NightTime.Minutes:D2}";

        public bool AutoBrightnessEnabled
        {
            get => _settings.AutoBrightnessEnabled;
            set
            {
                if (_settings.AutoBrightnessEnabled != value)
                {
                    _settings.AutoBrightnessEnabled = value;
                    OnPropertyChanged();
                    SaveSettings();
                    _scheduleService.EvaluateSchedule(force: true);
                }
            }
        }

        public uint DayBrightness
        {
            get => _settings.DayBrightness;
            set
            {
                if (_settings.DayBrightness != value)
                {
                    _settings.DayBrightness = Math.Clamp(value, 0, 100);
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public uint NightBrightness
        {
            get => _settings.NightBrightness;
            set
            {
                if (_settings.NightBrightness != value)
                {
                    _settings.NightBrightness = Math.Clamp(value, 0, 100);
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool LinkBrightnessToTheme
        {
            get => _settings.LinkBrightnessToTheme;
            set
            {
                if (_settings.LinkBrightnessToTheme != value)
                {
                    _settings.LinkBrightnessToTheme = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool StartWithWindows
        {
            get => _settings.StartWithWindows;
            set
            {
                if (_settings.StartWithWindows != value)
                {
                    _settings.StartWithWindows = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool StartMinimized
        {
            get => _settings.StartMinimized;
            set
            {
                if (_settings.StartMinimized != value)
                {
                    _settings.StartMinimized = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool MinimizeToTray
        {
            get => _settings.MinimizeToTray;
            set
            {
                if (_settings.MinimizeToTray != value)
                {
                    _settings.MinimizeToTray = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public void SaveSettings()
        {
            _settingsService.Save(_settings);
        }

        #endregion

        #region Windows Night Light Integration

        private NightLightScheduleInfo _nightLightInfo = new();

        public bool IsNightLightSupported => _nightLightService.IsSupported();

        public bool SyncWithNightLight
        {
            get => _settings.SyncWithNightLight;
            set
            {
                if (_settings.SyncWithNightLight != value)
                {
                    _settings.SyncWithNightLight = value;
                    OnPropertyChanged();
                    SaveSettings();
                    _scheduleService.ResetLastAppliedState();
                    _scheduleService.EvaluateSchedule(force: true);
                }
            }
        }

        public int NightLightSyncMode
        {
            get => _settings.NightLightSyncMode;
            set
            {
                if (_settings.NightLightSyncMode != value)
                {
                    _settings.NightLightSyncMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSyncModeFollowState));
                    OnPropertyChanged(nameof(IsSyncModeFollowSunset));
                    OnPropertyChanged(nameof(IsSyncModeDriveNightLight));
                    SaveSettings();
                    _scheduleService.ResetLastAppliedState();
                    _scheduleService.EvaluateSchedule(force: true);
                }
            }
        }

        public bool IsSyncModeFollowState
        {
            get => NightLightSyncMode == 0;
            set { if (value) NightLightSyncMode = 0; }
        }

        public bool IsSyncModeFollowSunset
        {
            get => NightLightSyncMode == 1;
            set { if (value) NightLightSyncMode = 1; }
        }

        public bool IsSyncModeDriveNightLight
        {
            get => NightLightSyncMode == 2;
            set { if (value) NightLightSyncMode = 2; }
        }

        public string NightLightStatusText
        {
            get
            {
                if (!IsNightLightSupported) return "Windows Night Light: Not available";
                bool active = _nightLightService.IsNightLightActive();
                return active ? "Night Light is currently ON" : "Night Light is currently OFF";
            }
        }

        public string SunsetText => _nightLightInfo.Sunset.HasValue
            ? $"{_nightLightInfo.Sunset.Value.Hours:D2}:{_nightLightInfo.Sunset.Value.Minutes:D2}"
            : "19:46";

        public string SunriseText => _nightLightInfo.Sunrise.HasValue
            ? $"{_nightLightInfo.Sunrise.Value.Hours:D2}:{_nightLightInfo.Sunrise.Value.Minutes:D2}"
            : "06:46";

        public void RefreshNightLightInfo()
        {
            _nightLightInfo = _nightLightService.GetNightLightInfo();
            OnPropertyChanged(nameof(IsNightLightSupported));
            OnPropertyChanged(nameof(NightLightStatusText));
            OnPropertyChanged(nameof(SunsetText));
            OnPropertyChanged(nameof(SunriseText));
        }

        public void ImportSunsetSunriseTimes()
        {
            RefreshNightLightInfo();
            if (_nightLightInfo.Sunset.HasValue && _nightLightInfo.Sunrise.HasValue)
            {
                DayHour = _nightLightInfo.Sunrise.Value.Hours;
                DayMinute = _nightLightInfo.Sunrise.Value.Minutes;
                NightHour = _nightLightInfo.Sunset.Value.Hours;
                NightMinute = _nightLightInfo.Sunset.Value.Minutes;
            }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
