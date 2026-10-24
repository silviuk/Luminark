using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
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
            private readonly SafePhysicalMonitorHandle[] _owned;
            public PhysicalMonitorGroup(NativeMethods.PHYSICAL_MONITOR[] monitors)
            {
                Monitors = monitors;
                _owned = monitors.Select(m => new SafePhysicalMonitorHandle(m.hPhysicalMonitor)).ToArray();
            }

            public void Destroy()
            {
                try
                {
                    if (Monitors != null && Monitors.Length > 0)
                    {
                        foreach (var handle in _owned) handle.Dispose();
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
        private readonly object _workersGate = new();
        private readonly Dictionary<string, LatestMonitorWorker> _workers = new();
        private readonly Dictionary<string, IntPtr> _physicalHandles = new();
        private sealed class BrightnessRequest(long version, uint value)
        {
            internal readonly long Version = version;
            internal readonly uint Value = value;
            internal int Completed;
        }
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, BrightnessRequest> _brightnessRequests = new();
        private long _brightnessRevision;
        private readonly Action<MonitorInfo, uint>? _brightnessOverride;
        private int _disposed;

        public MonitorService() { }
        internal MonitorService(Action<MonitorInfo, uint> brightnessOverride) => _brightnessOverride = brightnessOverride;

        private void Schedule(MonitorInfo monitor, MonitorCommandKind kind, Action apply)
        {
            lock (_workersGate)
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                if (!_workers.TryGetValue(monitor.Id, out var worker))
                    _workers[monitor.Id] = worker = new LatestMonitorWorker();
                worker.Schedule(kind, () =>
                {
                    // All native calls and handle replacement share this lifetime gate.
                    lock (_lock)
                    {
                        if (Volatile.Read(ref _disposed) == 0 && monitor.IsActive) apply();
                    }
                });
            }
        }

        private bool ResolveHandle(MonitorInfo monitor)
        {
            if (!_physicalHandles.TryGetValue(monitor.Id, out var handle)) return false;
            monitor.PhysicalHandle = handle;
            return handle != IntPtr.Zero;
        }

        internal uint GetDetectedBrightness(MonitorInfo detected)
        {
            if (_brightnessRequests.TryGetValue(detected.Id, out var request) &&
                (request.Version > detected.BrightnessRevision || Volatile.Read(ref request.Completed) == 0))
                return request.Value;
            return detected.CurrentBrightness;
        }

        internal void ObserveBrightness(MonitorInfo monitor, uint value)
        {
            _brightnessRequests[monitor.Id] = new BrightnessRequest(Interlocked.Increment(ref _brightnessRevision), value) { Completed = 1 };
        }

        public static string FormatStableId(string pnpId, string desc, int monitorIndex)
        {
            if (!string.IsNullOrWhiteSpace(pnpId))
            {
                var parts = pnpId.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string hwModel = parts[1];
                    string subId = parts.Length >= 3 ? parts[parts.Length - 1] : "";
                    string cleanKey = $"{hwModel}_{subId}".Replace("{", "").Replace("}", "").Replace("-", "_").Trim('_');
                    if (!string.IsNullOrWhiteSpace(cleanKey))
                    {
                        return $"DDC_{cleanKey}";
                    }
                }
            }

            string cleanDesc = desc.Replace(" ", "_").Trim();
            return $"DDC_{cleanDesc}_{monitorIndex}";
        }

        public List<MonitorInfo> EnumerateMonitors()
        {
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0) return new();
                var revisions = _brightnessRequests.ToDictionary(item => item.Key, item => item.Value.Version);
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
                            var mi = new NativeMethods.MONITORINFOEX();
                            mi.cbSize = Marshal.SizeOf(mi);
                            string devName = "";
                            if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                            {
                                devName = mi.szDevice ?? "";
                            }

                            if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) && count > 0)
                            {
                                var physMonitors = new NativeMethods.PHYSICAL_MONITOR[count];
                                if (NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physMonitors))
                                {
                                    foreach (var physical in physMonitors)
                                        _monitorGroups.Add(new PhysicalMonitorGroup(new[] { physical }));

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

                                        // Fallback to low-level VESA MCCS VCP opcode 0x10 if high-level API failed
                                        if (!hasBrightness)
                                        {
                                            try
                                            {
                                                uint pvct = 0, vcpCur = 0, vcpMax = 0;
                                                if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, 0x10, out pvct, out vcpCur, out vcpMax))
                                                {
                                                    hasBrightness = true;
                                                    min = 0;
                                                    cur = Math.Clamp(vcpCur, 0, 100);
                                                    max = vcpMax > 0 ? vcpMax : 100;
                                                    App.Log($"[MonitorService] Low-level VCP 0x10 brightness detected for {desc} (cur={cur}, max={max})");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                App.Log($"[MonitorService] VCP 0x10 check error for {desc}: {ex.Message}");
                                            }
                                        }

                                        string pnpId = "";
                                        try
                                        {
                                            var dd = new NativeMethods.DISPLAY_DEVICE();
                                            dd.cb = Marshal.SizeOf(dd);
                                            if (!string.IsNullOrEmpty(devName) && NativeMethods.EnumDisplayDevices(devName, (uint)i, ref dd, 0))
                                            {
                                                pnpId = dd.DeviceID ?? "";
                                            }
                                        }
                                        catch { }

                                        string stableId = FormatStableId(pnpId, desc, monitorIndex);

                                        if (hasBrightness)
                                        {
                                            uint volPvct = 0, curVol = 50, maxVol = 100;
                                            bool hasAudio = false;
                                            try
                                            {
                                                hasAudio = NativeMethods.GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, 0x62, out volPvct, out curVol, out maxVol);
                                            }
                                            catch { }

                                            uint mutePvct = 0, curMute = 0, maxMute = 0;
                                            bool hasMute = false;
                                            if (hasAudio)
                                            {
                                                try
                                                {
                                                    hasMute = NativeMethods.GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, 0x8D, out mutePvct, out curMute, out maxMute);
                                                }
                                                catch { }
                                            }

                                            uint inPvct = 0, curInput = 0, maxInput = 0;
                                            bool hasInput = false;
                                            try
                                            {
                                                hasInput = NativeMethods.GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, 0x60, out inPvct, out curInput, out maxInput);
                                            }
                                            catch { }

                                            var mon = new MonitorInfo
                                            {
                                                Id = stableId,
                                                DeviceName = desc,
                                                FriendlyName = desc,
                                                Type = MonitorType.DdcCi,
                                                PhysicalHandle = pm.hPhysicalMonitor,
                                                HMonitor = hMonitor,
                                                PnpDeviceId = pnpId,
                                                MinBrightness = min,
                                                MaxBrightness = max,
                                                CurrentBrightness = cur,
                                                SupportsAudioVolume = hasAudio,
                                                SupportsAudioMute = hasMute,
                                                MinVolume = 0,
                                                MaxVolume = maxVol > 0 ? maxVol : 100,
                                                CurrentVolume = hasAudio ? curVol : 50,
                                                IsMuted = hasMute && curMute == 1,
                                                SupportsInputSelect = false
                                            };

                                            // Query DDC/CI capabilities string if available
                                            var capsCodes = new List<uint>();
                                            try
                                            {
                                                if (NativeMethods.GetCapabilitiesStringLength(pm.hPhysicalMonitor, out uint capsLen) && capsLen > 0)
                                                {
                                                    var sb = new StringBuilder((int)capsLen + 1);
                                                    if (NativeMethods.CapabilitiesRequestAndCapabilitiesReply(pm.hPhysicalMonitor, sb, capsLen))
                                                    {
                                                        string caps = sb.ToString();
                                                        App.Log($"[MonitorService] Capabilities for {desc}: {caps}");
                                                        capsCodes = ParseCapabilitiesInputCodes(caps);
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                App.Log($"[MonitorService] Query capabilities error for {desc}: {ex.Message}");
                                            }

                                            bool supportsInput = hasInput || capsCodes.Count > 0;
                                            mon.SupportsInputSelect = supportsInput;

                                            if (supportsInput)
                                            {
                                                if (capsCodes.Count > 0)
                                                {
                                                    // Monitor explicitly reported its physical input list via MCCS capabilities
                                                    foreach (var code in capsCodes)
                                                    {
                                                        mon.AllInputOptions.Add(new MonitorInputOption
                                                        {
                                                            Code = code,
                                                            DefaultName = GetPortNameForCode(code),
                                                            IsVisibleInFlyout = true
                                                        });
                                                    }

                                                    if (hasInput && curInput > 0 && !capsCodes.Contains(curInput))
                                                    {
                                                        mon.AllInputOptions.Add(new MonitorInputOption
                                                        {
                                                            Code = curInput,
                                                            DefaultName = GetPortNameForCode(curInput),
                                                            IsVisibleInFlyout = true
                                                        });
                                                    }
                                                }
                                                else
                                                {
                                                    // Capabilities did not enumerate 0x60 codes, but VCP 0x60 replied to query
                                                    if (hasInput && curInput > 0)
                                                    {
                                                        mon.AllInputOptions.Add(new MonitorInputOption
                                                        {
                                                            Code = curInput,
                                                            DefaultName = GetPortNameForCode(curInput),
                                                            IsVisibleInFlyout = true
                                                        });
                                                    }

                                                    // Provide modern candidates for manual configuration in Settings,
                                                    // but keep unconfirmed candidates hidden from quick controls flyout
                                                    var candidates = GetModernCandidateInputOptions();
                                                    foreach (var opt in candidates)
                                                    {
                                                        if (!mon.AllInputOptions.Any(o => o.Code == opt.Code))
                                                        {
                                                            opt.IsVisibleInFlyout = (hasInput && curInput > 0) ? false : true;
                                                            mon.AllInputOptions.Add(opt);
                                                        }
                                                    }
                                                }

                                                if (hasInput && curInput > 0)
                                                {
                                                    mon.ActiveInputCode = curInput;
                                                    mon.SelectedInputCode = curInput;
                                                }
                                                else if (mon.AllInputOptions.Count > 0)
                                                {
                                                    mon.ActiveInputCode = mon.AllInputOptions[0].Code;
                                                    mon.SelectedInputCode = mon.AllInputOptions[0].Code;
                                                }

                                                foreach (var opt in mon.AllInputOptions)
                                                {
                                                    opt.IsConnected = (hasInput && curInput > 0 && opt.Code == curInput);
                                                }

                                                mon.UpdateVisibleInputOptions();
                                            }

                                            results.Add(mon);
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
                        using (obj)
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
                        }
                        wmiIdx++;
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"WMI Brightness enumeration failed: {ex.Message}");
                }

                var activeHandles = results.Select(m => m.PhysicalHandle).ToHashSet();
                foreach (var group in _monitorGroups.ToArray())
                {
                    if (!activeHandles.Contains(group.Monitors[0].hPhysicalMonitor))
                    {
                        group.Destroy();
                        _monitorGroups.Remove(group);
                    }
                }
                foreach (var monitor in results.Where(m => m.Type == MonitorType.DdcCi))
                    _physicalHandles[monitor.Id] = monitor.PhysicalHandle;
                var activeIds = results.Select(m => m.Id).ToHashSet();
                foreach (var monitor in results)
                    monitor.BrightnessRevision = revisions.GetValueOrDefault(monitor.Id);
                foreach (var id in _brightnessRequests.Keys.Where(id => !activeIds.Contains(id)))
                    _brightnessRequests.TryRemove(id, out _);
                lock (_workersGate)
                {
                    foreach (var id in _workers.Keys.Where(id => !activeIds.Contains(id)).ToArray())
                    {
                        _ = _workers[id].StopAsync();
                        _workers.Remove(id);
                    }
                }
                App.Log($"EnumerateMonitors completed. Found {results.Count} monitors.");
                return results;
            }
        }

        public void SetBrightness(MonitorInfo monitor, uint brightness)
        {
            if (monitor == null || !monitor.IsActive || Volatile.Read(ref _disposed) != 0) return;
            brightness = Math.Clamp(brightness, monitor.MinBrightness, monitor.MaxBrightness);
            monitor.CurrentBrightness = brightness;
            uint target = brightness;
            var request = new BrightnessRequest(Interlocked.Increment(ref _brightnessRevision), target);
            _brightnessRequests[monitor.Id] = request;
            Schedule(monitor, MonitorCommandKind.Brightness, () =>
            {
                try
                {
                    if (_brightnessOverride != null) _brightnessOverride(monitor, target);
                    else if (monitor.Type == MonitorType.DdcCi) ApplyDdcBrightness(monitor, target);
                    else if (monitor.Type == MonitorType.WmiInternal) SetWmiBrightness(target, monitor.InstanceName);
                }
                finally { Volatile.Write(ref request.Completed, 1); }
            });
        }
        private void ApplyDdcBrightness(MonitorInfo monitor, uint brightness)
        {
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0 || !ResolveHandle(monitor)) return;

                if (!monitor.IsActive) return;

                bool success = false;
                IntPtr handle = monitor.PhysicalHandle;

                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        // 1. Try High-Level SetMonitorBrightness
                        success = NativeMethods.SetMonitorBrightness(handle, brightness);
                        if (!success)
                        {
                            int err = Marshal.GetLastWin32Error();
                            // 2. Immediately try Low-Level VESA MCCS VCP 0x10 (Luminance)
                            success = NativeMethods.SetVCPFeature(handle, 0x10, brightness);
                            if (!success)
                            {
                                int vcpErr = Marshal.GetLastWin32Error();
                                App.Log($"[MonitorService] SetMonitorBrightness failed (0x{err:X8}), SetVCPFeature(0x10) failed (0x{vcpErr:X8}) on {monitor.FriendlyName} (handle={handle})");
                            }
                            else
                            {
                                App.Log($"[MonitorService] Low-level SetVCPFeature(0x10, {brightness}%) succeeded on {monitor.FriendlyName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[MonitorService] DDC/CI brightness exception on {monitor.FriendlyName}: {ex.Message}");
                    }
                }

                // 3. Handle re-acquisition if calls failed or handle was zero
                if (!success)
                {
                    App.Log($"[MonitorService] DDC/CI commands failed. Attempting handle re-acquisition for {monitor.FriendlyName} ({monitor.Id})...");
                    if (TryReacquirePhysicalHandle(monitor))
                    {
                        handle = monitor.PhysicalHandle;
                        if (handle != IntPtr.Zero)
                        {
                            // Retry with both low-level and high-level
                            success = NativeMethods.SetVCPFeature(handle, 0x10, brightness) ||
                                      NativeMethods.SetMonitorBrightness(handle, brightness);
                            App.Log($"[MonitorService] Re-acquired handle={handle}, retry brightness result={success}");
                        }
                    }
                }
            }
        }

        public bool TryReacquirePhysicalHandle(MonitorInfo monitor)
        {
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0 || !monitor.IsActive) return false;
                IntPtr found = IntPtr.Zero;
                NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    (IntPtr hMon, IntPtr hdc, ref NativeMethods.Rect rect, IntPtr data) =>
                    {
                        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
                        if (!NativeMethods.GetMonitorInfo(hMon, ref mi) ||
                            !NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) || count == 0)
                            return true;
                        var physical = new NativeMethods.PHYSICAL_MONITOR[count];
                        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMon, count, physical)) return true;
                        for (int i = 0; i < physical.Length; i++)
                        {
                            var candidate = physical[i];
                            bool matches = false;
                            if (found == IntPtr.Zero)
                            {
                                if (!string.IsNullOrEmpty(monitor.PnpDeviceId))
                                {
                                    var dd = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
                                    matches = NativeMethods.EnumDisplayDevices(mi.szDevice, (uint)i, ref dd, 0) &&
                                        string.Equals(dd.DeviceID, monitor.PnpDeviceId, StringComparison.OrdinalIgnoreCase);
                                }
                                else
                                {
                                    matches = monitor.HMonitor == hMon &&
                                        (physical.Length == 1 || string.Equals(candidate.szPhysicalMonitorDescription,
                                            monitor.FriendlyName, StringComparison.OrdinalIgnoreCase));
                                }
                            }
                            if (matches)
                            {
                                found = candidate.hPhysicalMonitor;
                                monitor.HMonitor = hMon;
                                foreach (var old in _monitorGroups.Where(g =>
                                    g.Monitors[0].hPhysicalMonitor == monitor.PhysicalHandle).ToArray())
                                {
                                    old.Destroy();
                                    _monitorGroups.Remove(old);
                                }
                                _monitorGroups.Add(new PhysicalMonitorGroup(new[] { candidate }));
                                monitor.PhysicalHandle = found;
                                _physicalHandles[monitor.Id] = found;
                            }
                            else NativeMethods.DestroyPhysicalMonitor(candidate.hPhysicalMonitor);
                        }
                        return found == IntPtr.Zero;
                    }, IntPtr.Zero);
                return found != IntPtr.Zero;
            }
        }
        public void SetAllBrightness(IEnumerable<MonitorInfo> monitors, uint brightness)
        {
            foreach (var mon in monitors)
            {
                if (!mon.IsActive) continue;
                SetBrightness(mon, brightness);
            }
        }

        public static List<MonitorInputOption> GetModernCandidateInputOptions()
        {
            return new List<MonitorInputOption>
            {
                new MonitorInputOption { Code = 0x11, DefaultName = "HDMI 1" },
                new MonitorInputOption { Code = 0x12, DefaultName = "HDMI 2" },
                new MonitorInputOption { Code = 0x0F, DefaultName = "DisplayPort 1" },
                new MonitorInputOption { Code = 0x10, DefaultName = "DisplayPort 2" },
                new MonitorInputOption { Code = 0x13, DefaultName = "USB-C" }
            };
        }

        public static List<MonitorInputOption> GetStandardInputOptions() => GetModernCandidateInputOptions();

        public static string GetPortNameForCode(uint code) => code switch
        {
            0x01 => "VGA 1",
            0x02 => "VGA 2",
            0x03 => "DVI 1",
            0x04 => "DVI 2",
            0x05 => "Composite 1",
            0x06 => "Composite 2",
            0x07 => "S-Video 1",
            0x08 => "S-Video 2",
            0x09 => "Tuner 1",
            0x0A => "Tuner 2",
            0x0B => "Tuner 3",
            0x0C => "Component 1",
            0x0D => "Component 2",
            0x0E => "Component 3",
            0x0F => "DisplayPort 1",
            0x10 => "DisplayPort 2",
            0x11 => "HDMI 1",
            0x12 => "HDMI 2",
            0x13 => "USB-C",
            _ => $"Input (0x{code:X2})"
        };

        public static List<uint> ParseCapabilitiesInputCodes(string caps)
        {
            var list = new List<uint>();
            if (string.IsNullOrWhiteSpace(caps)) return list;

            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(caps, @"\b60\s*\(\s*([^)]+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string inner = match.Groups[1].Value;
                    var tokens = inner.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var t in tokens)
                    {
                        string codeStr = t.Trim();
                        int eqIdx = codeStr.IndexOf('=');
                        if (eqIdx > 0)
                        {
                            codeStr = codeStr.Substring(0, eqIdx).Trim();
                        }
                        if (codeStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            codeStr = codeStr.Substring(2);
                        }
                        if (uint.TryParse(codeStr, System.Globalization.NumberStyles.HexNumber, null, out uint code))
                        {
                            if (!list.Contains(code))
                            {
                                list.Add(code);
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        public bool SetInputSource(MonitorInfo monitor, uint inputCode)
        {
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0 || !ResolveHandle(monitor)) return false;

                if (monitor.Type != MonitorType.DdcCi || monitor.PhysicalHandle == IntPtr.Zero || !monitor.IsActive)
                    return false;

                try
                {
                    App.Log($"[MonitorService] Setting input on {monitor.FriendlyName} to 0x{inputCode:X2}...");

                    uint valueToSend = inputCode;
                    // If the monitor previously returned high-byte info for VCP 60, preserve it
                    if (monitor.ActiveInputCode.HasValue && (monitor.ActiveInputCode.Value & 0xFF00) != 0 && (inputCode & 0xFF00) == 0)
                    {
                        valueToSend = (monitor.ActiveInputCode.Value & 0xFF00) | (inputCode & 0xFF);
                    }

                    bool ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x60, valueToSend);
                    if (!ok)
                    {
                        if (TryReacquirePhysicalHandle(monitor))
                        {
                            ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x60, valueToSend);
                        }
                    }

                    if (!ok && valueToSend != inputCode)
                    {
                        // Fall back to raw inputCode without preserved high-byte
                        ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x60, inputCode);
                    }

                    if (!ok)
                    {
                        // Brief delay and retry in case DDC/CI bus was busy
                        Thread.Sleep(50);
                        ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x60, inputCode);
                    }

                    App.Log($"[MonitorService] SetVCPFeature (0x60, 0x{inputCode:X2}) result: {ok}");
                    return ok;
                }
                catch (Exception ex)
                {
                    App.Log($"[MonitorService] Failed to set input on {monitor.FriendlyName}: {ex.Message}");
                    return false;
                }
            }
        }

        public uint? GetCurrentInputSource(MonitorInfo monitor)
        {
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0 || !ResolveHandle(monitor)) return null;

                if (monitor.Type != MonitorType.DdcCi || monitor.PhysicalHandle == IntPtr.Zero || !monitor.IsActive)
                    return null;

                try
                {
                    uint pvct = 0, cur = 0, max = 0;
                    if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(monitor.PhysicalHandle, 0x60, out pvct, out cur, out max))
                    {
                        return cur;
                    }
                    if (TryReacquirePhysicalHandle(monitor))
                    {
                        if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(monitor.PhysicalHandle, 0x60, out pvct, out cur, out max))
                        {
                            return cur;
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[MonitorService] GetCurrentInputSource error on {monitor.FriendlyName}: {ex.Message}");
                }
                return null;
            }
        }

        public void SetVolume(MonitorInfo monitor, uint volume, bool isMuted = false)
        {
            if (monitor.Type != MonitorType.DdcCi || !monitor.SupportsAudioVolume || !monitor.IsActive) return;
            uint target = Math.Clamp(volume, monitor.MinVolume, monitor.MaxVolume);
            Schedule(monitor, MonitorCommandKind.Volume, () =>
            {
                if (!ResolveHandle(monitor)) return;
                if (monitor.SupportsAudioMute)
                    NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x8D, isMuted ? 1u : 2u);
                NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x62, isMuted ? 0u : target);
            });
        }
        public void SetMonitorPowerMode(MonitorInfo monitor, uint powerMode)
        {
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0 || !ResolveHandle(monitor)) return;

                if (monitor.Type != MonitorType.DdcCi || !monitor.IsActive) return;
                try
                {
                    if (monitor.PhysicalHandle == IntPtr.Zero)
                    {
                        TryReacquirePhysicalHandle(monitor);
                    }
                    if (monitor.PhysicalHandle == IntPtr.Zero) return;

                    // VCP 0xD6: 0x01 = On, 0x02 = Standby, 0x03 = Suspend, 0x04 = Off.
                    // Never send 0x04 (Soft Power Off) as it turns off the scaler/USB-C PD controller on LG monitors,
                    // preventing wake-on-signal and requiring an AC mains power cycle. Map 0x04 to 0x02 (DPMS Standby).
                    uint safeMode = powerMode == 0x04 ? 0x02u : powerMode;
                    bool ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0xD6, safeMode);
                    if (!ok && TryReacquirePhysicalHandle(monitor))
                    {
                        ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0xD6, safeMode);
                    }
                    App.Log($"[MonitorService] SetMonitorPowerMode ({monitor.FriendlyName}, 0xD6, 0x{safeMode:X2}) result: {ok}");
                }
                catch (Exception ex)
                {
                    App.Log($"[MonitorService] SetMonitorPowerMode error on {monitor.FriendlyName}: {ex.Message}");
                }
            }
        }

        public void SetAllExternalMonitorsPowerMode(IEnumerable<MonitorInfo> monitors, uint powerMode)
        {
            var list = monitors.Where(m => m.Type == MonitorType.DdcCi && m.IsActive).ToList();
            foreach (var m in list)
            {
                SetMonitorPowerMode(m, powerMode);
            }
        }

        public void SetInternalBrightness(uint targetBrightness)
        {
            SetWmiBrightness(targetBrightness);
        }

        public uint? GetInternalBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightness");
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    using (obj)
                    {
                        if (obj["CurrentBrightness"] != null)
                        {
                            return Convert.ToUInt32(obj["CurrentBrightness"]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"Get WMI Brightness error: {ex.Message}");
            }
            return null;
        }

        private ManagementEventWatcher? _brightnessWatcher;
        public event EventHandler<uint>? InternalBrightnessChanged;

        public void StartWmiBrightnessWatcher()
        {
            try
            {
                var scope = new ManagementScope(@"root\wmi");
                scope.Connect();
                var query = new WqlEventQuery("WmiMonitorBrightnessEvent");
                _brightnessWatcher = new ManagementEventWatcher(scope, query);
                _brightnessWatcher.EventArrived += (s, e) =>
                {
                    try
                    {
                        // Observations update the model without writing back to WMI.
                        // Do not suppress hardware keys following an application write.
                        if (e.NewEvent != null && e.NewEvent["Brightness"] != null)
                        {
                            uint brightness = Convert.ToByte(e.NewEvent["Brightness"]);
                            InternalBrightnessChanged?.Invoke(this, brightness);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[MonitorService] Error handling WmiMonitorBrightnessEvent: {ex.Message}");
                    }
                };
                _brightnessWatcher.Start();
                App.Log("[MonitorService] WMI brightness event watcher started for hardware Fn keys.");
            }
            catch (Exception ex)
            {
                App.Log($"[MonitorService] WMI brightness event watcher not available: {ex.Message}");
            }
        }

        private void SetWmiBrightness(uint targetBrightness, string? targetInstance = null)
        {
            lock (_lock)
            {
                if (Volatile.Read(ref _disposed) != 0) return;

                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                    using var collection = searcher.Get();

                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)
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
                }
                catch (Exception ex)
                {
                    App.Log($"Set WMI Brightness error: {ex.Message}");
                }
            }
        }

        private void CleanupPhysicalMonitors()
        {
            foreach (var group in _monitorGroups)
            {
                group.Destroy();
            }
            _monitorGroups.Clear();
            _physicalHandles.Clear();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (_brightnessWatcher != null)
            {
                try { _brightnessWatcher.Stop(); _brightnessWatcher.Dispose(); } catch { }
                _brightnessWatcher = null;
            }
            Task drained;
            lock (_workersGate)
            {
                drained = Task.WhenAll(_workers.Values.Select(w => w.StopAsync()));
                _workers.Clear();
                _brightnessRequests.Clear();
            }
            // Native drivers cannot be cancelled. Never free their handles before I/O returns.
            // Let a slow driver finish off the UI thread rather than hanging shutdown.
            var cleanup = drained.ContinueWith(_ =>
            {
                lock (_lock) CleanupPhysicalMonitors();
            }, TaskScheduler.Default);
            cleanup.Wait(TimeSpan.FromSeconds(1));
        }
    }
}
