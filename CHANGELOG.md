# Changelog

All notable changes to Luminark are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.9] - 2026-09-22

### Fixed
- **Keep Laptop Awake During "Lock & Screen Off" on S0 Modern Standby**:
  - Resolved an issue on modern Windows laptops (S0 Low Power Idle / Connected Standby) where pressing "Lock & Screen Off" or turning off monitors resulted in Windows entering Modern Standby sleep despite "Never sleep when plugged in" and "Prevent sleep: Forever".
  - **Windows Away Mode Integration**: Registered `PowerRequestAwayModeRequired` via Kernel Power Request APIs (`kernel32.dll`) and `ES_AWAYMODE_REQUIRED` via `SetThreadExecutionState`. Away Mode ensures the system, CPU, disk, network, and desktop applications run at 100% capacity while screens and audio are powered down.
  - **Independent ThreadPool Heartbeat Timer**: Added a dedicated `System.Threading.Timer` running on the thread pool to reaffirm kernel power requests every 30 seconds, preventing Windows from suspending keep-alive state when the desktop switches to `LogonUI` (lock screen).
  - **VESA DDC/CI Hardware Standby**: External monitors are explicitly signaled to enter standby (`VCP 0xD6 = 4`) and automatically awakened (`VCP 0xD6 = 1`) upon session unlock.
  - **Targeted Monitor Power Commands**: Avoided shell-wide `HWND_BROADCAST` sleep triggers by directing display power-down commands to the application window.

---

## [1.1.8] - 2026-09-22

### Fixed
- **Prevent Unminimized Startup at Windows Boot**:
  - Resolved an issue where secondary or delayed startup triggers caused the main window to unexpectedly restore and pop up on screen at Windows login.
  - Guarded single-instance activation so secondary launches with `--minimized` (or packaged `StartupTask`) exit quietly and never activate the running tray instance.

### Improved
- **Autostart Consolidation & Cleanup**:
  - Standardized autostart strictly to the Windows `Run` registry key.
  - Added automated cleanup routines to detect and remove redundant or legacy shortcuts in `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`.

---

## [1.1.7] - 2026-09-20

### Added
- **Video Input Sources Customization & Filtering**:
  - Full management of external monitor video input sources (HDMI, DisplayPort, USB-C, DVI, VGA) in the Main Window.
  - Per-port checkboxes to choose exactly which inputs appear in the quick controls mini window dropdown.
  - User-defined friendly input names (e.g., *"Work Laptop"*, *"Gaming PC"*, *"PS5"*) with instant synchronization across the Main Window and flyout.
  - Connected signal indicator: green checkmark (`✔`) displayed next to inputs reporting an active video signal in both the Main Window and mini window dropdown.
- **Multi-Host Inactive Monitor Retention**:
  - When an external monitor is switched to another host/PC and removed from Windows desktop topology, Luminark retains the display marked with an *"Inactive (Other Input / Device)"* badge.
  - Input selection dropdown remains fully interactive on inactive displays, allowing users to switch back to this host with a single click.
- **Global Hotkey for Lock & Turn Off Screen**:
  - Added configurable global keyboard shortcut (`Win+J` by default) to instantly lock the workstation and power down monitors while maintaining system keep-alive.
  - Shortcut recorder, clear button, and defaults reset in Settings.
  - Shortcut key display string dynamically shown in the system tray context menu.

### Fixed
- **Tray Quick Controls Mini Window Positioning**:
  - Fixed anchor position issues across multi-monitor setups and varied taskbar alignments (top, bottom, left, right).
  - Accurate per-monitor DPI scaling and viewport boundary clamping to ensure 100% visibility at all times.

---

## [1.1.6] - 2026-09-19

### Added
- **Lock-Aware Sleep Prevention (Display Off While PC Awakens)**:
  - Intelligently detects workstation lock and unlock events (`SystemEvents.SessionSwitch` and `WTSQuerySessionInformationW`).
  - When workstation is locked (`Win + L` or screen saver lock):
    - Automatically releases display keep-alive requests (`PowerRequestDisplayRequired` and `ES_DISPLAY_REQUIRED`), allowing monitors to turn off and save power.
    - Pauses the 30-second heartbeat mouse nudge to avoid unwanted display wakeups.
    - Firmly retains kernel `PowerRequestSystemRequired` and `PowerRequestExecutionRequired` so CPU execution, background downloads, and active tasks continue uninterrupted without entering sleep or being throttled by Windows 11 Modern Standby (DAM).
  - When workstation is unlocked:
    - Instantly restores display keep-alive requests and resumes the heartbeat pulse.

---

## [1.1.5] - 2026-09-19

### Fixed
- **Startup Crash (Missing app.ico Resource)**:
  - Resolved `XamlParseException` on application startup caused by missing embedded `assets/app.ico` resource.
  - Added `assets\app.ico` to assembly `<Resource>` and `<Content>` item groups in project file.
  - Implemented safe, defensive window icon loading in code-behind with multi-tier fallback (pack URI -> relative file -> system default) to prevent startup failures.

---

## [1.1.4] - 2026-09-19

### Added
- **Kernel Power Availability Requests (Modern Standby / S0 Support)**:
  - Upgraded sleep prevention engine to modern Windows Kernel Power Requests (`PowerCreateRequest`, `PowerSetRequest`).
  - Bulletproof display and system sleep prevention on Windows 11 Modern Standby (S0 Low Power Idle) devices.
  - Prevents Desktop Activity Moderator (DAM) process throttling and background suspension.
  - Non-intrusive 30-second micro-input heartbeat pulse to keep legacy display idle timers refreshed.
  - Reaffirms awake state across OS power transitions and session switch events.
- **Automated .NET 8 Desktop Runtime Bootstrapping**:
  - Inno Setup installer now automatically detects missing .NET 8 Desktop Runtime (x64) and downloads/installs it silently during setup.
- **Native WinGet Dependencies**:
  - Declared `Microsoft.DotNet.DesktopRuntime.8` package dependency in WinGet manifests for automatic runtime resolution.

### Changed
- **Lightweight Standalone Installer**:
  - `Luminark-Setup-v1.1.4.exe` reduced from 53.2 MB to 7.48 MB (~86% reduction) using framework-dependent publishing.
- **Optimized Store MSIX Package**:
  - Stripped unused satellite localization languages and debug symbols to reduce package size.

### Fixed
- **Taskbar Icon Accent Background Plate**:
  - Full suite of unplated taskbar icon assets generated and mapped via Package Resource Index (`resources.pri`).

---

## [1.1.3] - 2026-09-14

### Fixed
- **Intelligent Night Shift & Dark Mode Synchronization**:
  - Fixed issue where schedule evaluation could switch Windows to Light Mode at night when Night Light was running on schedule.
- **Schedule-Aware Night Light Detection**:
  - Night Light state evaluation now accounts for scheduled time windows and sunset/sunrise, accurately reflecting Windows 11 Night Light active periods.
- **First-Launch Local Sun Discovery**:
  - On fresh installs, Luminark automatically initializes daylight hours using Windows location-based sunset and sunrise times.

---

## [1.1.2] - 2026-09-13

### Added
- Multi-monitor hardware brightness synchronization across monitors.
- Tray flyout brightness quick controls.

---

## [1.1.1] - 2026-09-11

### Fixed
- Startup registration and background task scheduler fixes.

---

## [1.1.0] - 2026-09-08

### Added
- Modern Fluent Windows 11 UI with Wpf.Ui controls.
- Dynamic theme synchronization with sunrise/sunset schedules.
- Hardware brightness control via DDC/CI and WMI.
