# <img src="assets/logo.png" width="36" height="36" valign="middle" alt="Luminark Logo" /> Luminark

> **Dynamic Windows 11 Theme, Night Light & Multi-Monitor Hardware Brightness Controller**

[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://github.com/silviuk/Luminark)
[![Runtime: .NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release: v1.0.0](https://img.shields.io/badge/Release-v1.0.0-orange)](https://github.com/silviuk/Luminark/releases/latest)

**Luminark** is a native Windows 11 desktop application designed to unify and automate your display environment. It dynamically synchronizes Windows system and app themes with scheduled day/night times or Windows Night Light, provides direct hardware multi-monitor brightness control via **DDC/CI** and **WMI**, and offers intuitive system tray gestures for effortless control.

---

## ✨ Features

- 🌓 **Dynamic Theme Automation**:
  - Automatically transitions Windows system and application themes between Light and Dark mode.
  - Set custom daytime/nighttime transition hours or link directly with **Windows Night Light** live status.
  - Automatically applies daytime and nighttime brightness targets alongside theme shifts.

- 💡 **Hardware Multi-Monitor Brightness Control**:
  - Direct hardware brightness control for external monitors via **DDC/CI** (I2C bus).
  - Native internal display panel brightness control on laptops via **WMI**.
  - Synchronized **Master Brightness** slider or independent per-display control.
  - Quick brightness preset buttons (**25%**, **50%**, **75%**, **100%**).

- 🖱️ **Taskbar Tray Scroll Wheel Control**:
  - Hover over the Luminark tray icon and **scroll the mouse wheel** up or down to adjust master display brightness in real-time by **±5%** without opening any window.

- ⚡ **Quick Controls Flyout**:
  - Clean Windows 11 flyout positioned right above the system tray.
  - Live status display showing the next scheduled theme transition time.
  - Master and individual monitor brightness sliders.
  - Quick toggle to link or unlink display brightness.

- 🔒 **Lock & Screen Off**:
  - Lock your workstation and instantly put connected monitors into low-power sleep with one click.
  - Configurable countdown delay (*Instant*, *3s*, *5s*, *10s*) configured in the Settings tab, featuring a live countdown and cancel option in the flyout.

- ☕ **Prevent Computer Sleep**:
  - One-click toggle accessible from both the mini flyout and main settings to keep your workstation and displays awake, preventing automatic idle sleep during long renders, downloads, or presentations.

- 🚀 **Lightweight & Efficient**:
  - Ultra-low memory footprint (~15–25 MB working set when minimized to tray).
  - **0.0% CPU** utilization at idle using event-driven Windows hooks and system notifications.

---

## 🎮 Shortcuts & Tray Gestures

| Action | Result |
| :--- | :--- |
| **Scroll Wheel over Tray Icon** | Adjust Master Brightness by **±5%** |
| **Double-Click Tray Icon** | Instantly toggle between **Dark Mode** and **Light Mode** |
| **Single-Click Tray Icon** | Open or toggle the **Quick Controls Flyout** |
| **Right-Click Tray Icon** | Open the **System Tray Context Menu** |

---

## 📥 Installation

### Option 1: Setup Installer (Recommended)
1. Download **`Luminark-Setup-v1.0.0.exe`** from [**Latest GitHub Releases**](https://github.com/silviuk/Luminark/releases/latest).
2. Run the installer (supports both standard user and administrative installation, plus silent `/VERYSILENT` deployments).
3. Luminark will launch automatically and reside quietly in your Windows System Tray.

### Option 2: Portable / Self-Contained
1. Download or extract the compiled binaries.
2. Run `Luminark.exe` directly without installation.

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Inno Setup 6 (optional, for packaging the installer)

### Build Steps
```powershell
# Clone the repository
git clone https://github.com/silviuk/Luminark.git
cd Luminark

# Restore dependencies and build
dotnet build -c Release

# Publish framework-dependent binaries
dotnet publish Luminark.csproj -c Release -o installer\publish

# (Optional) Compile Inno Setup installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\Luminark.iss
```

---

## 🏛️ Architecture & Interop

- **Framework**: .NET 8 WPF with C# 12
- **UI Design**: [WPF-UI](https://github.com/lepoco/wpfui) (Windows 11 Fluent Design System)
- **Hardware & Windows APIs**:
  - `dxva2.dll`: `GetMonitorBrightness`, `SetMonitorBrightness`, `GetPhysicalMonitorsFromHMONITOR`
  - `user32.dll`: `WH_MOUSE_LL` low-level mouse hook, `LockWorkStation`, `SendMessageTimeout`, `PostMessage` (`SC_MONITORPOWER`)
  - `kernel32.dll`: `SetThreadExecutionState` (`ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED`)
  - `shell32.dll`: `Shell_NotifyIconGetRect` bounding box query
  - `root\wmi`: `WmiMonitorBrightnessMethods` for internal laptop screens
  - Windows Registry: `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`

---

## 📄 License

Distributed under the [MIT License](LICENSE). Copyright © 2026 Silviu K.
