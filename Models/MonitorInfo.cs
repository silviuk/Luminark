using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lumina.Models
{
    public enum MonitorType
    {
        DdcCi,
        WmiInternal,
        Unknown
    }

    public class MonitorInfo : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public MonitorType Type { get; set; } = MonitorType.DdcCi;
        public IntPtr PhysicalHandle { get; set; } = IntPtr.Zero;
        public uint MinBrightness { get; set; } = 0;
        public uint MaxBrightness { get; set; } = 100;
        public string InstanceName { get; set; } = string.Empty;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
