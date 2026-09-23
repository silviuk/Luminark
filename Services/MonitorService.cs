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
                                                SupportsInputSelect = true
                                            };

                                            var standardInputs = GetStandardInputOptions();
                                            bool currentInList = false;
                                            foreach (var opt in standardInputs)
                                            {
                                                mon.AllInputOptions.Add(opt);
                                                if (hasInput && opt.Code == curInput)
                                                {
                                                    currentInList = true;
                                                }
                                            }

                                            // Query DDC/CI capabilities string if available
                                            try
                                            {
                                                if (NativeMethods.GetCapabilitiesStringLength(pm.hPhysicalMonitor, out uint capsLen) && capsLen > 0)
                                                {
                                                    var sb = new StringBuilder((int)capsLen + 1);
                                                    if (NativeMethods.CapabilitiesRequestAndCapabilitiesReply(pm.hPhysicalMonitor, sb, capsLen))
                                                    {
                                                        string caps = sb.ToString();
                                                        App.Log($"[MonitorService] Capabilities for {desc}: {caps}");
                                                        var capsCodes = ParseCapabilitiesInputCodes(caps);
                                                        foreach (var code in capsCodes)
                                                        {
                                                            if (!mon.AllInputOptions.Any(o => o.Code == code))
                                                            {
                                                                mon.AllInputOptions.Add(new MonitorInputOption
                                                                {
                                                                    Code = code,
                                                                    DefaultName = GetPortNameForCode(code)
                                                                });
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                App.Log($"[MonitorService] Query capabilities error for {desc}: {ex.Message}");
                                            }

                                            if (hasInput && curInput > 0 && !currentInList && !mon.AllInputOptions.Any(o => o.Code == curInput))
                                            {
                                                var customOpt = new MonitorInputOption
                                                {
                                                    Code = curInput,
                                                    DefaultName = GetPortNameForCode(curInput)
                                                };
                                                mon.AllInputOptions.Add(customOpt);
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
            if (monitor == null || !monitor.IsActive) return;

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

                        if (monitor.Type == MonitorType.DdcCi)
                        {
                            ApplyDdcBrightness(monitor, brightness);
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

        private void ApplyDdcBrightness(MonitorInfo monitor, uint brightness)
        {
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

        public bool TryReacquirePhysicalHandle(MonitorInfo monitor)
        {
            lock (_lock)
            {
                try
                {
                    IntPtr foundPhysHandle = IntPtr.Zero;

                    NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.Rect r, IntPtr d) =>
                    {
                        try
                        {
                            var mi = new NativeMethods.MONITORINFOEX();
                            mi.cbSize = Marshal.SizeOf(mi);
                            string devName = "";
                            if (NativeMethods.GetMonitorInfo(hMon, ref mi))
                            {
                                devName = mi.szDevice ?? "";
                            }

                            if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) && count > 0)
                            {
                                var physMonitors = new NativeMethods.PHYSICAL_MONITOR[count];
                                if (NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMon, count, physMonitors))
                                {
                                    _monitorGroups.Add(new PhysicalMonitorGroup(physMonitors));

                                    for (int i = 0; i < count; i++)
                                    {
                                        var pm = physMonitors[i];
                                        string desc = string.IsNullOrWhiteSpace(pm.szPhysicalMonitorDescription)
                                            ? ""
                                            : pm.szPhysicalMonitorDescription;

                                        bool matches = false;
                                        if (!string.IsNullOrEmpty(monitor.PnpDeviceId))
                                        {
                                            var dd = new NativeMethods.DISPLAY_DEVICE();
                                            dd.cb = Marshal.SizeOf(dd);
                                            if (NativeMethods.EnumDisplayDevices(devName, (uint)i, ref dd, 0))
                                            {
                                                if (string.Equals(dd.DeviceID, monitor.PnpDeviceId, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    matches = true;
                                                }
                                            }
                                        }

                                        if (!matches && monitor.HMonitor != IntPtr.Zero && monitor.HMonitor == hMon)
                                        {
                                            matches = true;
                                        }

                                        if (!matches && !string.IsNullOrEmpty(desc) &&
                                            (string.Equals(desc, monitor.FriendlyName, StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(desc, monitor.DeviceName, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            matches = true;
                                        }

                                        if (matches)
                                        {
                                            foundPhysHandle = pm.hPhysicalMonitor;
                                            monitor.PhysicalHandle = pm.hPhysicalMonitor;
                                            monitor.HMonitor = hMon;
                                            return false; // stop enumeration
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                        return true;
                    }, IntPtr.Zero);

                    return foundPhysHandle != IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    App.Log($"[MonitorService] TryReacquirePhysicalHandle error: {ex.Message}");
                    return false;
                }
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

        public static List<MonitorInputOption> GetStandardInputOptions()
        {
            return new List<MonitorInputOption>
            {
                new MonitorInputOption { Code = 0x11, DefaultName = "HDMI 1" },
                new MonitorInputOption { Code = 0x12, DefaultName = "HDMI 2" },
                new MonitorInputOption { Code = 0x0F, DefaultName = "DisplayPort 1" },
                new MonitorInputOption { Code = 0x10, DefaultName = "DisplayPort 2" },
                new MonitorInputOption { Code = 0x13, DefaultName = "USB-C" },
                new MonitorInputOption { Code = 0x03, DefaultName = "DVI 1" },
                new MonitorInputOption { Code = 0x01, DefaultName = "VGA 1" }
            };
        }

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
                int idx = caps.IndexOf("60(", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int start = idx + 3;
                    int end = caps.IndexOf(')', start);
                    if (end > start)
                    {
                        string inner = caps.Substring(start, end - start);
                        var tokens = inner.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var t in tokens)
                        {
                            if (uint.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out uint code))
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
            if (monitor.Type != MonitorType.DdcCi || monitor.PhysicalHandle == IntPtr.Zero || !monitor.IsActive)
                return false;

            try
            {
                App.Log($"[MonitorService] Setting input on {monitor.FriendlyName} to 0x{inputCode:X2}...");
                bool ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x60, inputCode);
                App.Log($"[MonitorService] SetVCPFeature (0x60, 0x{inputCode:X2}) result: {ok}");
                return ok;
            }
            catch (Exception ex)
            {
                App.Log($"[MonitorService] Failed to set input on {monitor.FriendlyName}: {ex.Message}");
                return false;
            }
        }

        public void SetVolume(MonitorInfo monitor, uint volume, bool isMuted = false)
        {
            if (monitor.Type != MonitorType.DdcCi || monitor.PhysicalHandle == IntPtr.Zero || !monitor.SupportsAudioVolume || !monitor.IsActive)
                return;

            volume = Math.Clamp(volume, monitor.MinVolume, monitor.MaxVolume);

            lock (_debounceTokens)
            {
                string key = monitor.Id + "_vol";
                if (_debounceTokens.TryGetValue(key, out var oldCts))
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                var cts = new CancellationTokenSource();
                _debounceTokens[key] = cts;

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(40, cts.Token);
                        if (cts.Token.IsCancellationRequested || !monitor.IsActive) return;

                        if (isMuted)
                        {
                            if (monitor.SupportsAudioMute)
                            {
                                NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x8D, 1);
                            }
                            NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x62, 0);
                        }
                        else
                        {
                            if (monitor.SupportsAudioMute)
                            {
                                NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x8D, 2);
                            }
                            NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x62, volume);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        App.Log($"[MonitorService] Failed to set volume on {monitor.FriendlyName}: {ex.Message}");
                    }
                }, cts.Token);
            }
        }

        public void SetMonitorPowerMode(MonitorInfo monitor, uint powerMode)
        {
            if (monitor.Type != MonitorType.DdcCi || monitor.PhysicalHandle == IntPtr.Zero || !monitor.IsActive) return;
            try
            {
                // VCP 0xD6: 0x01 = On, 0x02 = Standby, 0x03 = Suspend, 0x04 = Off
                bool ok = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0xD6, powerMode);
                App.Log($"[MonitorService] SetMonitorPowerMode ({monitor.FriendlyName}, 0xD6, 0x{powerMode:X2}) result: {ok}");
            }
            catch (Exception ex)
            {
                App.Log($"[MonitorService] SetMonitorPowerMode error on {monitor.FriendlyName}: {ex.Message}");
            }
        }

        public void SetAllExternalMonitorsPowerMode(IEnumerable<MonitorInfo> monitors, uint powerMode)
        {
            foreach (var m in monitors)
            {
                if (m.Type == MonitorType.DdcCi && m.PhysicalHandle != IntPtr.Zero && m.IsActive)
                {
                    SetMonitorPowerMode(m, powerMode);
                }
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
                    if (obj["CurrentBrightness"] != null)
                    {
                        return Convert.ToUInt32(obj["CurrentBrightness"]);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"Get WMI Brightness error: {ex.Message}");
            }
            return null;
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
