---
name: windows-ddcci-monitor-control
description: >-
  Expert guide and implementation reference for controlling external and internal monitor hardware
  in Windows via DDC/CI (dxva2.dll), VESA MCCS VCP opcodes, and WMI. Use when developing display
  utilities, brightness sliders, video input switchers, or multi-monitor management tools.
---

# Windows DDC/CI & Display Hardware Control Guide

This skill provides an engineering reference for communicating directly with computer monitor hardware in Windows applications without third-party drivers.

---

## 1. Architecture Overview: High-Level vs. Low-Level DDC/CI

Windows provides access to DDC/CI (Display Data Channel / Command Interface) over the I2C bus via `dxva2.dll`.

### The Two Communication Layers
1. **High-Level Monitor Configuration API**:
   - `GetMonitorBrightness(hPhysicalMonitor, out min, out cur, out max)`
   - `SetMonitorBrightness(hPhysicalMonitor, dwNewBrightness)`
   - *Limitation*: Relies on driver-level capability flags (`MC_CAPS_BRIGHTNESS`). On many laptop GPU drivers (Intel Iris Xe, NVIDIA Optimus, AMD switchable graphics), this high-level API **fails or is rejected when driving an external monitor as the sole active display** ("Second screen only" or clamshell mode with laptop lid closed).
2. **Low-Level VESA MCCS VCP API**:
   - `GetVCPFeatureAndVCPFeatureReply(hPhysicalMonitor, bVCPCode, out pvct, out cur, out max)`
   - `SetVCPFeature(hPhysicalMonitor, bVCPCode, dwNewValue)`
   - *Advantage*: Sends the raw VESA Monitor Control Command Set (MCCS) opcode directly over the I2C bus. Works reliably across single-display and multi-display configurations.

### Key VESA MCCS VCP Opcodes
| VCP Opcode | Feature | Common Values / Description |
| :--- | :--- | :--- |
| **`0x10`** | **Luminance (Brightness)** | `0` – `100` (percentage) |
| **`0x12`** | **Contrast** | `0` – `100` |
| **`0x60`** | **Input Source Select** | `0x01` (VGA 1), `0x03` (DVI 1), `0x0F` (DisplayPort 1), `0x10` (DisplayPort 2), `0x11` (HDMI 1), `0x12` (HDMI 2), `0x13` (USB-C) |
| **`0x62`** | **Audio Speaker Volume** | `0` – `100` |
| **`0x8D`** | **Audio Mute** | `1` = Muted, `2` = Unmuted |
| **`0xD6`** | **Power Mode (DPMS)** | `0x01` = On, `0x02` = Standby, `0x03` = Suspend, `0x04` = Power Off |

---

## 2. Robust Brightness Implementation (Dual-Layer Fallback)

Always use a two-tiered execution strategy: attempt high-level `SetMonitorBrightness`, and on failure immediately fall back to low-level `SetVCPFeature(handle, 0x10, brightness)`.

```csharp
public bool SetBrightness(IntPtr hPhysicalMonitor, uint brightness)
{
    if (hPhysicalMonitor == IntPtr.Zero) return false;

    // 1. Try High-Level API
    bool success = NativeMethods.SetMonitorBrightness(hPhysicalMonitor, brightness);
    if (!success)
    {
        // 2. Immediately fall back to Low-Level VESA MCCS Opcode 0x10
        success = NativeMethods.SetVCPFeature(hPhysicalMonitor, 0x10, brightness);
    }

    return success;
}
```

---

## 3. Persistent Monitor Identification (Avoiding GDI Renumbering Bugs)

### The GDI Display Renumbering Problem
Windows GDI assigns device names like `\\.\DISPLAY1`, `\\.\DISPLAY2`.
- When both a laptop screen and external monitor are connected:
  - Laptop: `\\.\DISPLAY1`
  - External Monitor: `\\.\DISPLAY2`
- When the laptop lid is closed or set to "Second screen only":
  - Windows disables the laptop screen and **renumbers the external monitor to `\\.\DISPLAY1`**.
- If your application constructs monitor IDs using `devName` (`DDC_DISPLAY2_...`), the exact same physical display changes its ID to `DDC_DISPLAY1_...`, causing zombie duplicate monitor entries and settings loss.

### Solution: Hardware PnP ID Resolution
Use `EnumDisplayDevices` with `devName` to retrieve the monitor's permanent hardware PnP ID:

```csharp
string pnpId = "";
var dd = new NativeMethods.DISPLAY_DEVICE();
dd.cb = Marshal.SizeOf(dd);
if (NativeMethods.EnumDisplayDevices(devName, (uint)physicalIndex, ref dd, 0))
{
    pnpId = dd.DeviceID ?? ""; // e.g. "MONITOR\DELA0BF\{4d36e96e-e325-11ce-bfc1-08002be10318}\0001"
}

// Generate stable, persistent key:
string stableId = FormatStableId(pnpId, desc, monitorIndex);
```

---

## 4. USB-C DP Alt Mode & Dock Link Training Delay

When monitors are plugged in via USB-C or Thunderbolt docks, Windows fires `WM_DISPLAYCHANGE` (`0x007E`) or `SystemEvents.DisplaySettingsChanged` before hardware link training is complete. Calling `GetPhysicalMonitorsFromHMONITOR` immediately returns 0 physical monitors or stale handles.

### Solution: Progressive 3-Stage Detection
```csharp
public async Task HandleDisplayChangeAsync(CancellationToken token)
{
    // Stage 1: Wait 2.0s for GPU link training to settle
    await Task.Delay(2000, token);
    RefreshMonitors();

    // Stage 2: Wait another 2.0s (4.0s total) for slow USB-C docks
    await Task.Delay(2000, token);
    RefreshMonitors();

    // Stage 3: If only 1 monitor was detected so far, wait an extra 2.0s (6.0s total)
    if (Monitors.Count <= 1)
    {
        await Task.Delay(2000, token);
        RefreshMonitors();
    }
}
```

---

## 5. Automatic Physical Handle Re-acquisition

Physical monitor handles returned by `GetPhysicalMonitorsFromHMONITOR` can be invalidated by Windows display mode changes or GPU power transitions.

Implement dynamic handle recovery:
```csharp
public bool TryReacquirePhysicalHandle(MonitorInfo monitor)
{
    IntPtr newHandle = IntPtr.Zero;
    NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.Rect r, IntPtr d) =>
    {
        if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) && count > 0)
        {
            var physMonitors = new NativeMethods.PHYSICAL_MONITOR[count];
            if (NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMon, count, physMonitors))
            {
                for (int i = 0; i < count; i++)
                {
                    // Match by PnP Device ID or HMONITOR
                    if (Matches(monitor, physMonitors[i], hMon))
                    {
                        monitor.PhysicalHandle = physMonitors[i].hPhysicalMonitor;
                        monitor.HMonitor = hMon;
                        return false; // Stop enumeration
                    }
                }
            }
        }
        return true;
    }, IntPtr.Zero);

    return monitor.PhysicalHandle != IntPtr.Zero;
}
```

---

## 6. Internal Laptop Panels via WMI

Internal displays (eDP / LVDS) do not use DDC/CI and must be queried and controlled via WMI:

- **Query**: `root\wmi: SELECT * FROM WmiMonitorBrightness` -> `CurrentBrightness`
- **Control**: `root\wmi: SELECT * FROM WmiMonitorBrightnessMethods` -> invoke `WmiSetBrightness(Timeout: 1, Brightness: value)`
