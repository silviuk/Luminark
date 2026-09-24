---
name: windows-modern-standby-sleep-prevention
description: >-
  Comprehensive guide and implementation reference for managing Windows power states, preventing
  unwanted sleep on Windows 10/11 S0 Modern Standby (Low Power Idle) devices, and powering off
  displays natively during workstation lock without triggering system sleep.
---

# Windows Modern Standby (S0) Sleep Prevention & Display Power-Down Guide

This skill provides an authoritative architectural guide on handling Windows power states, overcoming S0 Modern Standby limitations, and implementing reliable sleep prevention while allowing displays to power off.

---

## 1. Modern Standby (S0) vs. Legacy S3 Architecture

In legacy systems (S3 Sleep), the CPU stops executing code, RAM is placed in self-refresh, and sleep is distinct from display power management.

In **Windows 10 / 11 Modern Standby (S0 Low Power Idle / S0ix)**:
- The system is technically always "on" in a connected standby state.
- Windows uses the **Desktop Activity Moderator (DAM)** to pause execution of Win32 desktop apps and suspend background network activity to save power.
- **Critical Pitfall**: When a desktop application calls:
  ```csharp
  SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2);
  ```
  Windows intercepts `SC_MONITORPOWER (2)` as a **manual hardware sleep button press** on Modern Standby systems! It instantly transitions the SoC into S0ix Sleep, cutting Wi-Fi to D3 and pausing CPU execution, completely disregarding "Never sleep when plugged in" settings!

---

## 2. The Solution: Native Console Lock Display Off Timeout (`VIDEOCONLOCK`)

To turn displays completely off (backlight 100% off) while keeping the PC 100% awake:

### The Mechanism
Windows provides a dedicated power setting: **Console Lock Display Off Timeout** (`GUID_CONSOLE_LOCK_DISPLAY_OFF_TIMEOUT`). When the workstation is locked, Windows automatically powers off monitors after this duration without entering sleep.

### Implementation Steps via `powrprof.dll`
1. Read current active scheme and lock timeout:
   ```csharp
   [DllImport("powrprof.dll")]
   public static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

   [DllImport("powrprof.dll")]
   public static extern uint PowerReadACValueIndex(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingsGuid, ref Guid PowerSettingGuid, out uint AcValueIndex);
   ```
2. Temporarily set timeout to 1 second:
   ```csharp
   static readonly Guid GUID_VIDEO_SUBGROUP = new("7516b95f-f776-4464-8c53-06167f40cc99");
   static readonly Guid GUID_CONSOLE_LOCK_DISPLAY_OFF_TIMEOUT = new("8ec4b3a5-6868-48c2-be75-4f3044be88a7");

   PowerWriteACValueIndex(IntPtr.Zero, ref activeScheme, ref GUID_VIDEO_SUBGROUP, ref GUID_CONSOLE_LOCK_DISPLAY_OFF_TIMEOUT, 1);
   PowerSetActiveScheme(IntPtr.Zero, ref activeScheme);
   ```
3. Lock workstation:
   ```csharp
   LockWorkStation();
   ```
4. Displays power off cleanly after 1 second.
5. Re-arm a timer to restore the original timeout (e.g. 60s) so waking the PC gives the user full normal brightness and unlock duration.

---

## 3. Kernel Power Availability Requests (Modern S0 Keep-Awake)

Legacy `SetThreadExecutionState` is frequently bypassed by Windows 11 Modern Standby. Use the modern Kernel Power Availability Requests API via `kernel32.dll`:

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr PowerCreateRequest(ref REASON_CONTEXT Context);

[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool PowerSetRequest(IntPtr PowerRequest, POWER_REQUEST_TYPE RequestType);

[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool PowerClearRequest(IntPtr PowerRequest, POWER_REQUEST_TYPE RequestType);
```

### Essential Request Types
- **`PowerRequestSystemRequired`**: Keeps the system, memory, and devices powered.
- **`PowerRequestExecutionRequired`**: Prevents the Desktop Activity Moderator (DAM) from suspending background threads.
- **`PowerRequestDisplayRequired`**: Keeps the display powered on when the user is unlocked.
- **`PowerRequestAwayModeRequired`**: Explicitly signals Windows Away Mode, running the system at full capacity while screens/audio are dark.

---

## 4. Lock-Aware State Handling

When the workstation is locked (`Win+L` or screen saver):
- **Release** `PowerRequestDisplayRequired` so monitors can power off.
- **Maintain** `PowerRequestSystemRequired` and `PowerRequestExecutionRequired` so CPU execution and background tasks continue uninterrupted.
- Listen for session changes via `SystemEvents.SessionSwitch` or `WTSRegisterSessionNotification(hWnd, NOTIFY_FOR_THIS_SESSION)`.
