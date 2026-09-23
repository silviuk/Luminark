using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace Lumina.Models
{
    public enum MonitorType
    {
        DdcCi,
        WmiInternal,
        Unknown
    }

    public class MonitorInputOption : INotifyPropertyChanged
    {
        public uint Code { get; set; }
        public string DefaultName { get; set; } = string.Empty;

        private string? _customName;
        public string? CustomName
        {
            get => _customName;
            set
            {
                if (_customName != value)
                {
                    _customName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(HasCustomName));
                    OnConfigChanged?.Invoke();
                }
            }
        }

        public bool HasCustomName => !string.IsNullOrWhiteSpace(_customName);

        public string Name => !string.IsNullOrWhiteSpace(_customName) ? _customName : DefaultName;
        public string DisplayName => Name;

        public string PortCodeHex => $"0x{Code:X2}";

        private bool _isVisibleInFlyout = true;
        public bool IsVisibleInFlyout
        {
            get => _isVisibleInFlyout;
            set
            {
                if (_isVisibleInFlyout != value)
                {
                    _isVisibleInFlyout = value;
                    OnPropertyChanged();
                    OnConfigChanged?.Invoke();
                }
            }
        }

        private bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ConnectionStatusToolTip));
                }
            }
        }

        public string ConnectionStatusToolTip => IsConnected
            ? "Active / Connected signal detected"
            : "No active signal reported";

        public Action? OnConfigChanged { get; set; }

        public override string ToString() => Name;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MonitorInfo : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public MonitorType Type { get; set; } = MonitorType.DdcCi;
        public IntPtr PhysicalHandle { get; set; } = IntPtr.Zero;
        public IntPtr HMonitor { get; set; } = IntPtr.Zero;
        public string PnpDeviceId { get; set; } = string.Empty;
        public uint MinBrightness { get; set; } = 0;
        public uint MaxBrightness { get; set; } = 100;
        public string InstanceName { get; set; } = string.Empty;

        private string? _customName;
        public string? CustomName
        {
            get => _customName;
            set
            {
                if (_customName != value)
                {
                    _customName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(HasCustomName));
                }
            }
        }

        public bool HasCustomName => !string.IsNullOrWhiteSpace(_customName);

        public string DisplayName => !string.IsNullOrWhiteSpace(_customName)
            ? _customName
            : (!string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName : DeviceName);

        #region Brightness
        private uint _currentBrightness = 50;
        public uint CurrentBrightness
        {
            get => _currentBrightness;
            set
            {
                if (_currentBrightness != value)
                {
                    _currentBrightness = Math.Clamp(value, MinBrightness, MaxBrightness);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BrightnessPercentageText));
                }
            }
        }

        public string BrightnessPercentageText => $"{CurrentBrightness}%";

        public string DisplayTechnologyText => Type switch
        {
            MonitorType.DdcCi => "External (DDC/CI Hardware)",
            MonitorType.WmiInternal => "Internal Display (WMI)",
            _ => "Generic Display"
        };

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsInactive));
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(CardOpacity));
                }
            }
        }

        public bool IsInactive => !IsActive;
        public string StatusBadgeText => IsActive ? "Active" : "Inactive (Other Input / Device)";
        public double CardOpacity => IsActive ? 1.0 : 0.65;
        #endregion

        #region Audio Volume & Mute
        public bool SupportsAudioVolume { get; set; } = false;
        public bool SupportsAudioMute { get; set; } = false;
        public uint MinVolume { get; set; } = 0;
        public uint MaxVolume { get; set; } = 100;

        private uint _currentVolume = 50;
        public uint CurrentVolume
        {
            get => _currentVolume;
            set
            {
                if (_currentVolume != value)
                {
                    _currentVolume = Math.Clamp(value, MinVolume, MaxVolume);
                    if (_isMuted && _currentVolume > 0)
                    {
                        _isMuted = false;
                        OnPropertyChanged(nameof(IsMuted));
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VolumePercentageText));
                    OnPropertyChanged(nameof(VolumeIconSymbol));
                    OnPropertyChanged(nameof(MuteToolTip));
                    OnVolumeChanged?.Invoke(this, _currentVolume, _isMuted);
                }
            }
        }

        private bool _isMuted = false;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (_isMuted != value)
                {
                    _isMuted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VolumePercentageText));
                    OnPropertyChanged(nameof(VolumeIconSymbol));
                    OnPropertyChanged(nameof(MuteToolTip));
                }
            }
        }

        private uint _savedVolumeBeforeMute = 50;

        public void ToggleMute()
        {
            if (!SupportsAudioVolume) return;

            if (IsMuted)
            {
                IsMuted = false;
                uint restoreVol = _savedVolumeBeforeMute > 0 ? _savedVolumeBeforeMute : 30;
                CurrentVolume = restoreVol;
            }
            else
            {
                _savedVolumeBeforeMute = CurrentVolume > 0 ? CurrentVolume : 30;
                IsMuted = true;
                OnVolumeChanged?.Invoke(this, 0, true);
                OnPropertyChanged(nameof(VolumePercentageText));
                OnPropertyChanged(nameof(VolumeIconSymbol));
                OnPropertyChanged(nameof(MuteToolTip));
            }
        }

        public string VolumePercentageText => IsMuted ? "Muted" : $"{CurrentVolume}%";
        public string VolumeIconSymbol => IsMuted ? "SpeakerMute24" : (CurrentVolume == 0 ? "SpeakerOff24" : "Speaker224");
        public string MuteToolTip => IsMuted ? "Unmute Audio" : "Mute Audio";

        public Action<MonitorInfo, uint, bool>? OnVolumeChanged { get; set; }
        #endregion

        #region Input Selection & Timer
        public bool SupportsInputSelect { get; set; } = false;
        public ObservableCollection<MonitorInputOption> AllInputOptions { get; } = new();
        public ObservableCollection<MonitorInputOption> InputOptions { get; } = new();

        private uint? _activeInputCode;
        public uint? ActiveInputCode
        {
            get => _activeInputCode;
            set
            {
                if (_activeInputCode != value)
                {
                    _activeInputCode = value;
                    OnPropertyChanged();

                    foreach (var opt in AllInputOptions)
                    {
                        opt.IsConnected = (_activeInputCode.HasValue && opt.Code == _activeInputCode.Value);
                    }

                    if (!IsSwitchingInput)
                    {
                        _selectedInputCode = value;
                        OnPropertyChanged(nameof(SelectedInputCode));
                    }
                }
            }
        }

        private uint? _selectedInputCode;
        public uint? SelectedInputCode
        {
            get => _selectedInputCode;
            set
            {
                if (_selectedInputCode != value)
                {
                    _selectedInputCode = value;
                    OnPropertyChanged();
                    OnInputSelectionRequested?.Invoke(this, value);
                }
            }
        }

        public void UpdateVisibleInputOptions()
        {
            var visibleList = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(AllInputOptions, o => o.IsVisibleInFlyout));
            if (visibleList.Count == 0 && AllInputOptions.Count > 0)
            {
                visibleList = System.Linq.Enumerable.ToList(AllInputOptions);
            }

            InputOptions.Clear();
            foreach (var item in visibleList)
            {
                InputOptions.Add(item);
            }

            if (SelectedInputCode.HasValue && !System.Linq.Enumerable.Any(InputOptions, o => o.Code == SelectedInputCode.Value))
            {
                if (ActiveInputCode.HasValue && System.Linq.Enumerable.Any(InputOptions, o => o.Code == ActiveInputCode.Value))
                {
                    SelectedInputCode = ActiveInputCode;
                }
                else if (InputOptions.Count > 0)
                {
                    SelectedInputCode = InputOptions[0].Code;
                }
            }
        }

        public void ResetInputName(MonitorInputOption opt)
        {
            opt.CustomName = null;
        }

        private DispatcherTimer? _inputCountdownTimer;
        private bool _isSwitchingInput = false;
        public bool IsSwitchingInput
        {
            get => _isSwitchingInput;
            private set
            {
                if (_isSwitchingInput != value)
                {
                    _isSwitchingInput = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _inputCountdownRemaining = 3;
        public int InputCountdownRemaining
        {
            get => _inputCountdownRemaining;
            private set
            {
                if (_inputCountdownRemaining != value)
                {
                    _inputCountdownRemaining = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InputCountdownText));
                }
            }
        }

        private string _pendingInputName = string.Empty;
        public string InputCountdownText => $"Switching to {_pendingInputName} in {InputCountdownRemaining}s... [Esc to cancel]";

        public Action<MonitorInfo, uint?>? OnInputSelectionRequested { get; set; }

        public void StartInputCountdown(uint targetCode, string targetName, int durationSeconds, Action<MonitorInfo, uint> onExecute)
        {
            _inputCountdownTimer?.Stop();
            _pendingInputName = targetName;
            InputCountdownRemaining = durationSeconds > 0 ? durationSeconds : 3;
            IsSwitchingInput = true;

            _inputCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _inputCountdownTimer.Tick += (s, e) =>
            {
                InputCountdownRemaining--;
                if (InputCountdownRemaining <= 0)
                {
                    _inputCountdownTimer.Stop();
                    IsSwitchingInput = false;
                    _activeInputCode = targetCode;
                    OnPropertyChanged(nameof(ActiveInputCode));
                    foreach (var opt in AllInputOptions)
                    {
                        opt.IsConnected = (opt.Code == targetCode);
                    }
                    onExecute(this, targetCode);
                }
            };
            _inputCountdownTimer.Start();
        }

        public void CancelInputSwitch()
        {
            if (IsSwitchingInput)
            {
                _inputCountdownTimer?.Stop();
                IsSwitchingInput = false;
                // Revert SelectedInputCode back to ActiveInputCode without re-triggering request
                _selectedInputCode = ActiveInputCode;
                OnPropertyChanged(nameof(SelectedInputCode));
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
