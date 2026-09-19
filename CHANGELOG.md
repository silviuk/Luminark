# Changelog

All notable changes to Luminark are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
