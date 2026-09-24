# Luminark: Project Prompt History & Evolution

This document chronicles the conversational prompts, architectural decisions, and evolutionary milestones that created and refined **Luminark** from scratch. 

Each milestone is mapped to its corresponding GitHub release tag, detailing the **user prompt**, **technical context**, **architectural resolution**, and a **reusable prompt pattern** that can be directly applied to other Windows desktop, WPF, power management, or hardware-interfacing projects.

---

## Table of Contents
1. [Release & Prompt Mapping Overview](#release--prompt-mapping-overview)
2. [v1.0.0: Project Inception & Core Architecture](#v100-project-inception--core-architecture)
3. [v1.0.1: Fluent Theme, Icons & Contrast Fixes](#v101-fluent-theme-icons--contrast-fixes)
4. [v1.0.2 – v1.0.3: Tray Customization & Global Hotkeys](#v102--v103-tray-customization--global-hotkeys)
5. [v1.0.4 – v1.0.5: Automated CI/CD for Store & WinGet](#v104--v105-automated-cicd-for-store--winget)
6. [v1.0.6 – v1.0.7: Video Input Switching & Monitor Renaming](#v106--v107-video-input-switching--monitor-renaming)
7. [v1.1.0: Progressive Detection & Multi-Host Display Retention](#v110-progressive-detection--multi-host-display-retention)
8. [v1.1.1: Autostart & Awake State Resiliency](#v111-autostart--awake-state-resiliency)
9. [v1.1.2: Self-Contained Zero-Dependency Store Packaging](#v112-self-contained-zero-dependency-store-packaging)
10. [v1.1.3 – v1.1.4: Modern Standby (S0) Kernel Power Requests](#v113--v114-modern-standby-s0-kernel-power-requests)
11. [v1.1.5: Defensive Resource & Asset Loading](#v115-defensive-resource--asset-loading)
12. [v1.1.6: Lock-Aware Display Power Down](#v116-lock-aware-display-power-down)
13. [v1.1.7: Per-Port Input Filtering & Global Lock Hotkey](#v117-per-port-input-filtering--global-lock-hotkey)
14. [v1.1.8: Boot Startup Window Suppression](#v118-boot-startup-window-suppression)
15. [v1.1.9 – v1.1.11: Modern Standby Sleep Override & Console Lock Screen Off](#v119--v1111-modern-standby-sleep-override--console-lock-screen-off)
16. [v1.1.12: Single-Display Laptop DDC/CI VCP Fallback & PnP IDs](#v1112-single-display-laptop-ddcci-vcp-fallback--pnp-ids)
17. [Reusable Prompt Patterns Catalog](#reusable-prompt-patterns-catalog)

---

## Release & Prompt Mapping Overview

| Tag | Date | Focus Area | Key Technical Innovations |
| :--- | :--- | :--- | :--- |
| **[v1.0.0](#v100-project-inception--core-architecture)** | 2026-09-06 | Genesis & MVP | WPF .NET 8, DDC/CI Dxva2, WMI brightness, System Tray, Low-level mouse scroll hook (`WH_MOUSE_LL`) |
| **[v1.0.1](#v101-fluent-theme-icons--contrast-fixes)** | 2026-09-08 | UI / UX Polish | Fluent theme synchronization, high-contrast light mode styling, Segoe Fluent Icons resolution |
| **[v1.0.2 - v1.0.3](#v102--v103-tray-customization--global-hotkeys)** | 2026-09-08 | Ergonomics | Configurable tray double-click behavior, Win32 `RegisterHotKey` global shortcuts |
| **[v1.0.4 - v1.0.5](#v104--v105-automated-cicd-for-store--winget)** | 2026-09-13 | Release Engineering | Automated Microsoft Store Partner Center API submission & WinGet PR pipeline |
| **[v1.0.6 - v1.0.7](#v106--v107-video-input-switching--monitor-renaming)** | 2026-09-13 | Display Control | DDC/CI VCP opcode 0x60 input switching, countdown cancellation timer, custom monitor renaming |
| **[v1.1.0](#v110-progressive-detection--multi-host-display-retention)** | 2026-09-13 | Multi-Display Architecture | Progressive 3-stage USB-C link training delay (2s/4s/6s), multi-host inactive monitor retention |
| **[v1.1.1](#v111-autostart--awake-state-resiliency)** | 2026-09-13 | Reliability | Registry `Run` autostart fix, state-machine initialization on boot |
| **[v1.1.2](#v112-self-contained-zero-dependency-store-packaging)** | 2026-09-14 | Distribution | Self-contained x64 deployment for Windows Store MSIX, eliminating .NET runtime prompts |
| **[v1.1.3 - v1.1.4](#v113--v114-modern-standby-s0-kernel-power-requests)** | 2026-09-19 | Power & Installer | Kernel Power Availability Requests (`PowerCreateRequest`), Inno Setup .NET bootstrapper |
| **[v1.1.5](#v115-defensive-resource--asset-loading)** | 2026-09-19 | Resiliency | Multi-tier defensive icon loader (Pack URI -> Local file -> System default) |
| **[v1.1.6](#v116-lock-aware-display-power-down)** | 2026-09-19 | Lock Management | Session switch detection (`WTSQuerySessionInformationW`), display keep-alive release on lock |
| **[v1.1.7](#v117-per-port-input-filtering--global-lock-hotkey)** | 2026-09-20 | Input & UX | Per-port visibility filter checkboxes, active signal indicators (`✔`), DPI-aware positioning |
| **[v1.1.8](#v118-boot-startup-window-suppression)** | 2026-09-22 | Boot Experience | Suppression of secondary single-instance activation on Windows login |
| **[v1.1.9 - v1.1.11](#v119--v1111-modern-standby-sleep-override--console-lock-screen-off)** | 2026-09-22 | Modern Standby (S0) | Elimination of `SC_MONITORPOWER` S0 sleep trigger; native Console Lock Display Timeout (`VIDEOCONLOCK`) screen off |
| **[v1.1.12](#v1112-single-display-laptop-ddcci-vcp-fallback--pnp-ids)** | 2026-09-23 | DDC/CI Hardware | Low-level VESA MCCS VCP 0x10 fallback for single external monitors; persistent PnP Hardware IDs |

---

## v1.0.0: Project Inception & Core Architecture

### User Prompts
```text
Write a windows program that:
1. Automatically enables dark mode or bright mode for windows and applications based on the local time of the day or a schedule.
2. Offers an easy way to change brightness of each and all connected monitors using DDC/CI.
3. Is a native Windows 11 application built with latest Windows APIs and feel.

Add the option to synchronize with windows night light.
Make sure the app has a GUI and a tray icon.
1. Make a modern, scalable icon.
2. When I scroll up/down on the tray icon, increase/decrease the brightness.
3. When I double click on the icon, toggle the mode.
4. When I single click on the icon, show a mini window next to the tray that has a brightness slider for each monitor, a button to link all monitors to the same brightness slider, a button to toggle dark/light mode, an icon/short text showing the automation mode, and the time of the next mode toggle.
Add in the mini window a button that turns off the monitors and locks the screen. Make the delay for this action configurable.
Add an option in the mini window to configure quickly how long the computer will be prevented from sleep (30 min, 1h, 2h, 4h, 8h, forever).
Rename the app to Luminark.
```

### Context & Implementation
- **Tech Stack**: C# / WPF on .NET 8, `Wpf.Ui` for Windows 11 Fluent Design styles (Mica backdrop, rounded corners, Segoe Fluent Icons).
- **Display Brightness**: P/Invoke to `dxva2.dll` (`EnumDisplayMonitors`, `GetPhysicalMonitorsFromHMONITOR`, `SetMonitorBrightness`) for external monitors and WMI (`root\wmi: WmiMonitorBrightnessMethods`) for internal laptop panels.
- **Tray Scroll Interaction**: Implemented low-level mouse hook (`WH_MOUSE_LL` via `SetWindowsHookEx`) in `Services/TrayScrollHook.cs` to capture `WM_MOUSEWHEEL` events specifically when hovering over the taskbar notification icon area.
- **Sleep Prevention**: Initial implementation using `SetThreadExecutionState` (`ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED`).

### Reusable Prompt Pattern
> *"Create a Windows 11 native tray utility in C# WPF (.NET 8) using modern Fluent controls (Mica, rounded corners). The utility must live in the system tray, support scrolling over the tray icon via a low-level mouse hook to adjust a master slider, provide a flyout anchored to the taskbar showing per-monitor controls, and interface with hardware monitors using dxva2 DDC/CI and internal screens via WMI."*

---

## v1.0.1: Fluent Theme, Icons & Contrast Fixes

### User Prompts
```text
First some fixes:
1. The colors in the main window are no longer changed correctly in the light mode, there's a black frame and an ugly gray. Please fix them/make them look as before, with the windows-style dark mode colors and fonts.
2. Mini window issues: in both modes, the various icons in the frame and the toggle buttons look like unrecognized characters (small rectangles). And some UI elements overlap. Additionally, in dark mode, there is some black text over dark background. Please fix and make a new release 1.0.1 on github.
```

### Context & Implementation
- **Issue**: Segoe Fluent Icons glyphs failed to render on machines lacking specific fonts; XAML theme resource dictionaries had hardcoded brushes causing unreadable black-on-dark text in dark mode and dark frames in light mode.
- **Resolution**: Replaced raw unicode string glyphs with native `Wpf.Ui.Controls.SymbolIcon` / `SymbolRegular` bindings. Reworked theme dictionaries to bind dynamically to `DynamicResource TextFillColorPrimaryBrush` and `CardBackgroundFillColorDefaultBrush`.

### Reusable Prompt Pattern
> *"Refactor the application's XAML styles to support seamless switching between Windows Light and Dark themes. Replace hardcoded brush colors with system theme dynamic resources, and ensure all icons use scalable vector symbols or standard Fluent icon controls instead of hardcoded unicode characters to prevent missing glyph squares."*

---

## v1.0.2 – v1.0.3: Tray Customization & Global Hotkeys

### User Prompts
```text
The version number in the settings tab on main page doesn't follow the github release version.
Also add a setting to decide if double click on tray icon does the mode change or opens the main window.
Add main window setting to configure global keyboard shortcuts (e.g. ctrl+alt+d for dark mode, ctrl+alt+l for light mode).
```

### Context & Implementation
- **Global Hotkeys**: Implemented `Services/HotkeyService.cs` using Win32 `RegisterHotKey` and `UnregisterHotKey` with an `HwndSource` hook in the main window.
- **Tray Action Disambiguation**: Added `TrayDoubleClickAction` setting (`ToggleTheme` vs `OpenMainWindow`) with dynamic click routing.

### Reusable Prompt Pattern
> *"Implement user-configurable global system-wide hotkeys using Win32 RegisterHotKey with an HwndSource message filter. Provide a settings UI allowing the user to record, clear, and persist custom modifier + key combinations."*

---

## v1.0.4 – v1.0.5: Automated CI/CD for Store & WinGet

### User Prompts
```text
Set up a github action that, when triggered manually, it publishes the latest release to Windows Store and Winget.
Is the partner center publisher ID the windows publisher id?
Is the azure app registration client id the application (client) ID in Entra?
MS_STORE_APP_ID - which one of these?
```

### Context & Implementation
- Created `.github/workflows/publish-store-and-winget.yml`.
- Integrated Microsoft Partner Center Submission API via Entra ID OAuth 2.0 token acquisition (`https://login.microsoftonline.com/{tenant_id}/oauth2/token`).
- Implemented automated draft submission creation, MSIX upload to Azure Blob Storage block blobs, listing update with release notes, and submission commit (`CommitStarted`).
- Automated WinGet manifest pull requests using `wingetcreate`.

### Reusable Prompt Pattern
> *"Create a GitHub Actions workflow that can be triggered on demand to publish a desktop MSIX package to the Microsoft Store. Authenticate with Microsoft Partner Center using an Azure Entra ID Service Principal, create a new draft submission, upload the MSIX bundle to Azure Blob Storage, update the listing changelog, and commit the submission for certification."*

---

## v1.0.6 – v1.0.7: Video Input Switching & Monitor Renaming

### User Prompts
```text
In the mini window, when independent monitors are shown, add under each one a dropdown that allows to select the input on each monitor. This should activate a timer similar to the lock button above, which triggers the monitor change after 3 seconds, or cancels it if Esc is pressed.
For monitors that support it, also add a volume slider in the mini window that allows to change the monitor sound volume (or mute it).
Add a main window setting to rename the monitors, a setting to select duration before monitor input change when clicked (e.g. 0 (no delay), 1, 3, 5 seconds).
In the mini window and right click menu on systray icon, add a way to force re-detecting monitors. Also, re-detect monitors whenever the PC is connected to a new monitor, or the display (extend, duplicate, turn off) changes.
```

### Context & Implementation
- **VCP Opcodes**:
  - `0x60`: Video Input Source (HDMI 1/2, DisplayPort 1/2, USB-C, DVI, VGA).
  - `0x62`: Audio Volume.
  - `0x8D`: Audio Mute.
- **Interactive Countdown**: Implemented `StartInputCountdown` on `MonitorInfo` with a dispatch timer and cancellation token triggered by `Esc` or manual cancel.
- **Dynamic Topology Watcher**: Bound `SystemEvents.DisplaySettingsChanged` and window message `WM_DISPLAYCHANGE` (`0x007E`) to trigger automatic re-enumeration.

### Reusable Prompt Pattern
> *"Add external monitor video input switching and audio volume control over DDC/CI using VESA MCCS opcodes 0x60 (input select), 0x62 (volume), and 0x8D (mute). Provide a safe input-switching countdown mechanism that gives users 3 seconds to cancel with Esc before switching inputs."*

---

## v1.1.0: Progressive Detection & Multi-Host Display Retention

### User Prompts
```text
Why when I plug in the USB-C monitor I had detected before Luminark doesn't detect it? Is the event of the monitor being connected not coming? Is Luminark trying too quickly to detect it before it's fully loaded? If the latter, I'm fine with a 1-3 seconds additional delay, but Luminark should see both laptop and external monitor automatically without me having to force re-discovery.
Make a new release and bump the version to 1.1. Then attempt to run the action that submits the latest release to Windows Store and Winget, and make sure the push includes also the changes and new features.
```

### Context & Implementation
- **Root Cause**: USB-C DisplayPort Alternate Mode, HDMI link training, and docking station I2C bridges take 2.5–3.5 seconds to settle. Querying immediately on `WM_DISPLAYCHANGE` returned 0 physical monitors.
- **Progressive Detection Algorithm**: Implemented a 3-stage detection sequence in `MainViewModel.cs`:
  - Stage 1: Wait 2.0s -> initial scan.
  - Stage 2: Wait another 2.0s (4.0s total) -> catches slow docks/monitors.
  - Stage 3: If only 1 monitor was detected so far, wait an additional 2.0s (6.0s total) for exceptionally slow high-resolution displays.
- **Multi-Host Display Retention**: When an external monitor is switched away to another host, Luminark retains it as `IsActive = false` (marked *"Inactive"*). This preserves the input dropdown so the user can switch back with one click.

### Reusable Prompt Pattern
> *"Implement a multi-stage progressive hardware detection algorithm for external monitors connected via USB-C or docking stations. When a display change event fires, debounce and sample the display topology at 2.0s, 4.0s, and 6.0s to allow hardware link training to complete. Retain disconnected or switched-away displays in an 'Inactive' state with interactive controls so users can switch inputs back."*

---

## v1.1.1: Autostart & Awake State Resiliency

### User Prompts
```text
Please investigate 2 possible issues:
1. Luminark doesn't autostart with Windows, despite being in the startup apps list.
2. The "prevent sleep" button appears not to have an effect, at least in some scenarios. It may be that if it starts by default on "prevent sleep for <duration>", e.g. "prevent sleep forever", it doesn't actually activate the sleep prevention unless manually selected again.
Make a 1.1.1 release and push to microsoft store.
```

### Context & Implementation
- **Autostart Fix**: Standardized registry key registration at `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with full quoted executable path and `--minimized` argument.
- **State Initialization**: Ensured `Settings.PreventSleep` unconditionally executes `ApplySleepPreventionState()` upon app startup, initializing kernel requests without requiring user interaction.

---

## v1.1.2: Self-Contained Zero-Dependency Store Packaging

### User Prompts
```text
When I install Luminark on a new computer from Windows store, it keeps asking me to "install or update .NET to run this application". When I click on "Download it now", it takes me to "Windows Desktop Runtime 8.0.31 (x64)", which if I download and install it, it asks me to repair it (sign that it was installed already). Even if I repair it, or if I uninstall, or re-install it, Luminark still asks to install or update .NET and thus never starts.
```

### Context & Implementation
- **Root Cause**: Windows Store packaged MSIX apps running under app container virtualization often fail to locate the host machine's globally installed .NET Desktop Runtime due to architecture and registry isolation.
- **Resolution**: Updated `build-and-release.yml` to publish self-contained for the MSIX distribution:
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained true -o publish-selfcontained
  ```
  Users downloading from the Microsoft Store obtain a 100% self-contained application with zero external runtime prerequisites.

### Reusable Prompt Pattern
> *"Configure .NET 8 desktop MSIX packaging for the Microsoft Store to be published as self-contained (win-x64), bundling the runtime inside the package to eliminate runtime discovery errors and missing .NET prompts on fresh Windows machines."*

---

## v1.1.3 – v1.1.4: Modern Standby (S0) Kernel Power Requests

### User Prompts
```text
Instead of including the runtimes inside the installation package, can we just make sure these packages are correctly referenced and requested for installation?
If Luminark starts first time after installation at night during night shift and dark mode, it should not change to light mode.
Luminark icon in taskbar has a background of the accent color in windows. It's not supposed to be there.
It seems that the app doesn't manage to prevent sleep, despite explicitly unticking and then ticking again the toggle. Please investigate why this happens, it's true also on this machine.
```

### Context & Implementation
- **Kernel Power Requests Upgrade**: Legacy `SetThreadExecutionState` is frequently ignored by Windows 11 Modern Standby (S0 Low Power Idle) and Desktop Activity Moderator (DAM).
- Upgraded to Windows Kernel Power Availability Requests via P/Invoke to `kernel32.dll`:
  - `PowerCreateRequest`
  - `PowerSetRequest(REASON_CONTEXT, PowerRequestSystemRequired)`
  - `PowerSetRequest(REASON_CONTEXT, PowerRequestDisplayRequired)`
  - `PowerSetRequest(REASON_CONTEXT, PowerRequestExecutionRequired)`
- Added a 30-second low-impact micro-input pulse to reset legacy idle counters.
- Fixed taskbar accent backplate by generating unplated transparent icon assets.

### Reusable Prompt Pattern
> *"Upgrade application sleep prevention from legacy SetThreadExecutionState to modern Windows Kernel Power Requests (PowerCreateRequest, PowerSetRequest with SystemRequired, DisplayRequired, and ExecutionRequired) to reliably keep Windows 11 S0 Modern Standby devices awake."*

---

## v1.1.5: Defensive Resource & Asset Loading

### User Prompts
```text
After installing Luminark from .exe I get this: [XamlParseException: Cannot locate resource 'assets/app.ico']
```

### Context & Implementation
- **Issue**: Different build pipelines (Inno Setup vs MSIX) packaged assembly resources differently; if `app.ico` was loaded as a strict Pack URI in XAML, any missing assembly metadata caused fatal startup crashes.
- **Resolution**: Implemented multi-tier defensive icon resolution in `Services/IconHelper.cs`:
  1. Primary: Assembly pack URI (`pack://application:,,,/assets/app.ico`).
  2. Secondary: File-system relative path (`Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "app.ico")`).
  3. Tertiary: Win32 process executable icon (`System.Drawing.Icon.ExtractAssociatedIcon`).
  4. Fallback: Standard `SystemIcons.Application`.

---

## v1.1.6: Lock-Aware Display Power Down

### User Prompts
```text
Check how windows powertoys handles the stay awake logic and compare with how Luminark does it.
Yes, Luminark should keep the PC awake even when the screen is locked and monitors turned off.
Release new version of package with changelog.
```

### Context & Implementation
- **Problem**: Users want the PC to stay awake (downloads running, CPU active), but want monitors to turn off when locking the workstation (`Win+L`).
- **Implementation**:
  - Listened to `SystemEvents.SessionSwitch` (`SessionLock` and `SessionUnlock`).
  - On Lock: Release `PowerRequestDisplayRequired` while firmly maintaining `PowerRequestSystemRequired` and `PowerRequestExecutionRequired`.
  - On Unlock: Re-acquire `PowerRequestDisplayRequired`.

---

## v1.1.7: Per-Port Input Filtering & Global Lock Hotkey

### User Prompts
```text
Sometimes the mini window appears in strange places. It should always appear next to the tray icon, fully visible.
Can you add win+j or another configurable keyboard shortcut for calling the lock and turn screen off feature?
In the main window, under each (external) monitor, show all monitor entries and allow with a tickbox for each to select which ones would be visible in the miniwindow selection dropdown. Also, allow to change their names to user defined ones. Also, if the monitor can report which ones currently have connected inputs, show a green checkmark after their name.
If possible, when a monitor has an input connected physically to the host on which Luminark runs, but it's not showing that input but another one (e.g. from another PC), still allow to see that monitor and its input selections.
```

### Context & Implementation
- **Tray Positioning**: Built DPI-aware window placement in `TrayFlyoutWindow.xaml.cs` using `GetDpiForMonitor`, querying taskbar orientation and bounding rect via `SHAppBarMessage(ABM_GETTASKBARPOS)`. Clamped window bounds to ensure 100% viewport visibility on all monitor arrangements.
- **Input Customization**: Added `CustomInputNames` and `VisibleInputCodes` dictionaries in `AppSettings.cs`. Rendered input checkboxes and friendly name editors with instant synchronization.
- **Signal Status**: Checked active video signal via DDC/CI capabilities query (`CapabilitiesRequestAndCapabilitiesReply`) and active VCP 0x60 status, rendering a green checkmark (`✔`) on connected ports.

---

## v1.1.8: Boot Startup Window Suppression

### User Prompts
```text
The app seems to start unminimized at boot on this machine.
Publish new package versions.
```

### Context & Implementation
- **Issue**: On Windows boot, secondary startup notifications or packaged `StartupTask` activation triggered the single-instance IPC handler, causing the window to restore itself.
- **Resolution**: Enhanced single-instance IPC in `App.xaml.cs`: If the secondary instance was invoked with `--minimized` or `--autostart`, it silently terminates without signaling the primary instance to restore its main window.

---

## v1.1.9 – v1.1.11: Modern Standby Sleep Override & Console Lock Screen Off

### User Prompts
```text
My laptops are configured in windows power settings to never sleep when plugged in. Luminark settings are prevent sleep on and forever.
Yet when I press lock and screen off, the laptops end up sleeping. Investigate why and propose a reliable fix.

This just doesn't work. When I click on lock and screen off, it goes to sleep right away.

This is not right. The laptop screen is now put at 0%, which is not exactly turned off, but barely visible. When I want to input the password to unlock, it's still not very visible.
I need you to propose a solution that actually works while turning the monitor off, so it keeps the PC on with the monitor off and screen locked. And publish new packages, but don't push yet to Windows store until I test it.

Make a summary changelog focused on user-relevant new features and fixes since the latest published version on Windows store and submit the latest package for publication in the store together with the summary changelog.
```

### Context & Deep Root Cause Analysis
1. **The Modern Standby (S0) Trap**:
   - On Windows 10/11 laptops with S0 Modern Standby, sending `SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2)` is intercepted by the Windows Power Manager as an explicit user sleep request! Windows immediately places the SoC into S0ix Sleep, cutting network and suspending background tasks.
2. **The 0% Dimming Workaround (Rejected)**:
   - Dimming the internal screen to 0% left backlights on and made unlocking difficult.
3. **The True Native Solution (`VIDEOCONLOCK`)**:
   - Windows has a built-in power setting called **Console Lock Display Off Timeout** (`GUID_CONSOLE_LOCK_DISPLAY_OFF_TIMEOUT`: `8EC4B3A5-6868-48c2-BE75-4F3044BE88A7` under `GUID_VIDEO_SUBGROUP`).
   - When the screen is locked, Windows automatically powers down all displays once this timeout elapses, **without entering system sleep**.
   - **Luminark's Implementation**:
     1. Reads the current lock timeout via `PowerReadACValueIndex`.
     2. Temporarily sets the timeout to 1 second via `PowerWriteACValueIndex` and `PowerSetActiveScheme`.
     3. Calls `LockWorkStation()`.
     4. Windows natively powers off all displays (backlights 100% off).
     5. Re-arms a timer that restores the original lock timeout (e.g. 60 seconds) so that when the user touches any key or mouse, the lock screen wakes immediately at full normal brightness!

### Reusable Prompt Pattern
> *"In Windows 11 laptops with S0 Modern Standby, sending SC_MONITORPOWER (2) forces the machine to sleep. Implement screen power-down on lock that keeps the system 100% awake by temporarily setting Windows Console Lock Display Off Timeout (GUID_CONSOLE_LOCK_DISPLAY_OFF_TIMEOUT via powrprof.dll) to 1 second before locking, and restoring the original timeout value after display shutdown."*

---

## v1.1.12: Single-Display Laptop DDC/CI VCP Fallback & PnP IDs

### User Prompts
```text
On one of my laptops, brightness control works only when both the external monitor and laptop screen are used. If I use only the external monitor, luminark detects it, but brightness changes don't have any effect.
Submit the updated package to windows store.
```

### Context & Deep Root Cause Analysis
1. **High-Level API Failure on Single-Display / Optimus**:
   - `NativeMethods.SetMonitorBrightness` (High-Level API) fails silently on many GPU drivers (Intel Iris Xe, NVIDIA Optimus, AMD) when driving an external monitor as the sole display ("Second screen only" or clamshell mode with laptop lid closed).
   - In contrast, the low-level VESA MCCS API `NativeMethods.SetVCPFeature(handle, 0x10, brightness)` sends the raw Luminance opcode `0x10` directly across I2C and succeeds.
2. **GDI Adapter Renumbering & Zombie Monitors**:
   - In dual-screen mode, the external monitor is `\\.\DISPLAY2`. When switching to single-screen mode, GDI renumbers the active display to `\\.\DISPLAY1`.
   - Luminark previously generated IDs using `DISPLAY1`/`DISPLAY2`, treating the display as a new monitor while retaining `DISPLAY2` as an inactive monitor with a destroyed handle.
   - Master Brightness was dispatching commands to the destroyed handle on `DISPLAY2`, clogging the I2C bus.
3. **Resolution**:
   - **Low-Level Fallback**: In `SetBrightness`, try `SetMonitorBrightness`. If it fails, immediately fall back to `SetVCPFeature(0x10)`.
   - **Persistent PnP Hardware IDs**: Extracted hardware PnP IDs via `EnumDisplayDevices` (e.g. `MONITOR\DELA0BF\...`), rendering monitor IDs completely immune to GDI display renumbering.
   - **Automatic Handle Re-acquisition**: If calls fail, re-query physical monitor handles for that display and retry.
   - **Inactive Filtering**: Master Brightness and batch operations strictly skip inactive displays (`!mon.IsActive`).

### Reusable Prompt Pattern
> *"Fix DDC/CI monitor brightness control failing when an external monitor is used as the sole display on a laptop. Add automatic fallback to low-level VESA MCCS VCP opcode 0x10 (SetVCPFeature) when high-level SetMonitorBrightness fails. Replace dynamic GDI display adapter names (DISPLAY1/DISPLAY2) with persistent PnP Hardware IDs from EnumDisplayDevices to avoid zombie duplicate monitors when display topology changes."*

---

## Reusable Prompt Patterns Catalog

Below is a consolidated reference of prompt patterns developed across Luminark that can be directly applied to future Windows engineering tasks:

### 1. Reliable Hardware DDC/CI Display Control
```text
Implement monitor brightness and feature control over DDC/CI using Windows dxva2.dll.
Ensure dual-mode operation: first attempt high-level SetMonitorBrightness; if that fails or returns false, immediately fall back to low-level VESA MCCS SetVCPFeature with opcode 0x10.
Identify monitors using persistent PnP Device IDs from EnumDisplayDevices rather than transient GDI display numbers (DISPLAY1/DISPLAY2) to prevent display renumbering bugs during topology changes.
Implement automatic handle re-acquisition if a physical handle becomes invalid after sleep/wake or dock reconnections.
```

### 2. Windows 11 Modern Standby (S0) Screen Off Without Sleep
```text
Implement a workstation lock and screen-off feature for Windows 10/11 that keeps the PC 100% awake on S0 Modern Standby laptops.
Do NOT use SC_MONITORPOWER (2), as Windows intercepts it as a sleep command on S0 systems.
Instead, use powrprof.dll to temporarily set the Windows Console Lock Display Off Timeout (GUID_CONSOLE_LOCK_DISPLAY_OFF_TIMEOUT: 8EC4B3A5-6868-48c2-BE75-4F3044BE88A7) to 1 second, lock the workstation with LockWorkStation(), and restore the original timeout once displays power down.
Combine this with Kernel Power Availability Requests (PowerRequestSystemRequired and PowerRequestExecutionRequired) to ensure downloads and background tasks continue running.
```

### 3. Progressive Display Link Training Detection
```text
When handling Windows display change events (WM_DISPLAYCHANGE or SystemEvents.DisplaySettingsChanged), implement progressive multi-stage scanning (e.g., 2.0s, 4.0s, and 6.0s) to allow USB-C DisplayPort Alt Mode and docking station link training to settle before querying physical monitor handles.
```

### 4. Fully Automated Microsoft Store Partner Center CI/CD
```text
Create a GitHub Actions workflow to publish desktop MSIX packages directly to the Microsoft Store via the Partner Center API.
Use an Azure Entra ID Service Principal to obtain an access token, query the application, create a draft submission, upload the self-contained MSIX bundle to Azure Blob Storage, update the Store listing's 'What's New' release notes, and commit the submission for certification.
```
