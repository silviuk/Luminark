using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Models;

namespace Lumina.Services
{
    public class MonitorService : IDisposable
    {
        private class PhysicalMonitorGroup
        {
            public NativeMethods.PHYSICAL_MONITOR[] Monitors { get; }
            public PhysicalMonitorGroup(NativeMethods.PHYSICAL_MONITOR[] monitors)
            {
                Monitors = monitors;
            }

            public void Destroy()
            {
                try
                {
                    if (Monitors != null && Monitors.Length > 0)
                    {
                        NativeMethods.DestroyPhysicalMonitors((uint)Monitors.Length, Monitors);
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"Error destroying physical monitor group: {ex.Message}");
                }
            }
        }

        private readonly List<PhysicalMonitorGroup> _monitorGroups = new();
        private readonly object _lock = new();
        private readonly Dictionary<string, CancellationTokenSource> _debounceTokens = new();

        public List<MonitorInfo> EnumerateMonitors()
        {
            lock (_lock)
            {
                App.Log("EnumerateMonitors: started");
                CleanupPhysicalMonitors();
                var results = new List<MonitorInfo>();
                int monitorIndex = 1;

                // 1. External / DDC/CI monitors via dxva2
                try
                {
                    NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.Rect r, IntPtr d) =>
                    {
                        try
                        {
                            if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) && count > 0)
                            {
                                var physMonitors = new NativeMethods.PHYSICAL_MONITOR[count];
                                if (NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physMonitors))
                                {
                                    _monitorGroups.Add(new PhysicalMonitorGroup(physMonitors));

                                    for (int i = 0; i < count; i++)
                                    {
                                        var pm = physMonitors[i];
                                        uint min = 0, cur = 50, max = 100;
                                        bool hasBrightness = false;
                                        try
                                        {
                                            hasBrightness = NativeMethods.GetMonitorBrightness(pm.hPhysicalMonitor, out min, out cur, out max);
                                        }
                                        catch (Exception ex)
                                        {
                                            App.Log($"GetMonitorBrightness failed for monitor {monitorIndex}: {ex.Message}");
                                        }

                                        string desc = string.IsNullOrWhiteSpace(pm.szPhysicalMonitorDescription)
                                            ? $"Display {monitorIndex}"
                                            : pm.szPhysicalMonitorDescription;

                                        if (hasBrightness)
                                        {
                                            results.Add(new MonitorInfo
                                            {
                                                Id = $"DDC_{pm.hPhysicalMonitor}_{monitorIndex}",
                                                DeviceName = desc,
                                                FriendlyName = desc,
                                                Type = MonitorType.DdcCi,
                                                PhysicalHandle = pm.hPhysicalMonitor,
                                                MinBrightness = min,
                                                MaxBrightness = max,
                                                CurrentBrightness = cur
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Log($"EnumDisplayMonitors callback error: {ex.Message}");
                        }
                        monitorIndex++;
                        return true;
                    }, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    App.Log($"EnumDisplayMonitors outer error: {ex.Message}");
                }

                // 2. Internal / laptop displays via WMI
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightness");
                    using var collection = searcher.Get();
                    int wmiIdx = 1;

                    foreach (ManagementObject obj in collection)
                    {
                        try
                        {
                            string instanceName = obj["InstanceName"]?.ToString() ?? $"InternalDisplay_{wmiIdx}";
                            uint currentBrightness = 50;

                            if (obj["CurrentBrightness"] != null)
                            {
                                currentBrightness = Convert.ToUInt32(obj["CurrentBrightness"]);
                            }

                            string friendly = "Built-in Display";
                            if (instanceName.Contains("DISPLAY\\", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = instanceName.Split('\\');
                                if (parts.Length > 1)
                                {
                                    friendly = $"Laptop Screen ({parts[1]})";
                                }
                            }

                            results.Add(new MonitorInfo
                            {
                                Id = $"WMI_{instanceName}",
                                DeviceName = instanceName,
                                FriendlyName = friendly,
                                Type = MonitorType.WmiInternal,
                                InstanceName = instanceName,
                                MinBrightness = 0,
                                MaxBrightness = 100,
                                CurrentBrightness = currentBrightness
                            });
                        }
                        catch (Exception ex)
                        {
                            App.Log($"WMI item read error: {ex.Message}");
                        }
                        wmiIdx++;
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"WMI Brightness enumeration failed: {ex.Message}");
                }

                App.Log($"EnumerateMonitors completed. Found {results.Count} monitors.");
                return results;
            }
        }

        public void SetBrightness(MonitorInfo monitor, uint brightness)
        {
            brightness = Math.Clamp(brightness, monitor.MinBrightness, monitor.MaxBrightness);
            monitor.CurrentBrightness = brightness;

            lock (_debounceTokens)
            {
                if (_debounceTokens.TryGetValue(monitor.Id, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                var cts = new CancellationTokenSource();
                _debounceTokens[monitor.Id] = cts;

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(40, cts.Token);
                        if (cts.Token.IsCancellationRequested) return;

                        if (monitor.Type == MonitorType.DdcCi && monitor.PhysicalHandle != IntPtr.Zero)
                        {
                            NativeMethods.SetMonitorBrightness(monitor.PhysicalHandle, brightness);
                        }
                        else if (monitor.Type == MonitorType.WmiInternal)
                        {
                            SetWmiBrightness(brightness, monitor.InstanceName);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        App.Log($"Failed to set brightness for {monitor.FriendlyName}: {ex.Message}");
                    }
                }, cts.Token);
            }
        }

        public void SetAllBrightness(IEnumerable<MonitorInfo> monitors, uint brightness)
        {
            foreach (var mon in monitors)
            {
                SetBrightness(mon, brightness);
            }
        }

        private static void SetWmiBrightness(uint targetBrightness, string? targetInstance = null)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    string? inst = obj["InstanceName"]?.ToString();
                    if (targetInstance == null || inst == targetInstance)
                    {
                        using var inParams = obj.GetMethodParameters("WmiSetBrightness");
                        inParams["Timeout"] = 1;
                        inParams["Brightness"] = (byte)targetBrightness;
                        obj.InvokeMethod("WmiSetBrightness", inParams, null);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"Set WMI Brightness error: {ex.Message}");
            }
        }

        private void CleanupPhysicalMonitors()
        {
            foreach (var group in _monitorGroups)
            {
                group.Destroy();
            }
            _monitorGroups.Clear();
        }

        public void Dispose()
        {
            lock (_lock)
            {
                CleanupPhysicalMonitors();
            }
            lock (_debounceTokens)
            {
                foreach (var cts in _debounceTokens.Values)
                {
                    try { cts.Cancel(); } catch { }
                    try { cts.Dispose(); } catch { }
                }
                _debounceTokens.Clear();
            }
        }
    }
}
