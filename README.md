# <img src="assets/logo.png" width="36" height="36" valign="middle" alt="Luminark Logo" /> Luminark

> **Dynamic Windows 11 Theme, Night Light & Multi-Monitor Hardware Brightness Controller**

[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011-0078D4?logo=windows11&logoColor=white)](https://github.com/silviuk/Luminark)
[![Runtime: .NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release: v1.1.1](https://img.shields.io/badge/Release-v1.1.1-orange)](https://github.com/silviuk/Luminark/releases/latest)

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

- 🔄 **Monitor Video Input Source Switching**:
  - Switch monitor inputs (DisplayPort, HDMI 1/2, USB-C) right from the flyout.
  - Configurable safety countdown delay (*0s/instant, 1s, 3s, 5s*) with an instant cancel button or `Esc` key press.

- 🔊 **Hardware Audio Volume & Mute Control**:
  - Adjust monitor built-in speaker volume or mute audio over DDC/CI directly from the flyout.

- 🏷️ **Custom Monitor Renaming & Stable Display IDs**:
  - Assign friendly custom names to each monitor from the Settings tab.
  - Persistent hardware IDs guarantee names survive USB-C reconnections, docks, and system reboots.

- 🔌 **Intelligent Display Hotplug & Auto-Detection**:
  - Event-driven hardware detection using Windows `WM_DISPLAYCHANGE` and `WM_DEVICECHANGE` (`DBT_DEVNODES_CHANGED`).
  - Progressive multi-stage discovery automatically accommodates USB-C Alternate Mode and DisplayPort link training delays without manual intervention.
  - Instant manual **"Re-detect Displays"** option available in the tray menu and flyout header.

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
  - Ultra-low memory footprint (~1–5 MB working set when minimized to tray).
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
1. Download **`Luminark-Setup-v1.1.1.exe`** from [**Latest GitHub Releases**](https://github.com/silviuk/Luminark/releases/latest).
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
  - `user32.dll`: `WH_MOUSE_LL` low-level mouse hook, `LockWorkStation`, `SendMessageTimeout`, `PostMessage` (`SC_MONITORPOWER`), `RegisterHotKey`
  - `kernel32.dll`: `SetThreadExecutionState` (`ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED`)
  - `shell32.dll`: `Shell_NotifyIconGetRect` bounding box query
  - `root\wmi`: `WmiMonitorBrightnessMethods` for internal laptop screens
  - Windows Registry: `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`

---

## 🌐 Store & WinGet Publishing

Luminark includes a dedicated GitHub Action workflow (`.github/workflows/publish-store-and-winget.yml`) that can be manually dispatched to publish any release directly to the **Windows Store** and **WinGet**:

### How to Trigger:
1. Go to your repository on GitHub and click the **Actions** tab.
2. Select the **"Publish to Microsoft Store and WinGet"** workflow on the left sidebar.
3. Click **"Run workflow"**:
   - **Release Tag**: Leave empty to auto-detect and publish the latest release, or specify a tag (e.g. `v1.0.4`).
   - **Publish to WinGet**: Checkbox (default: enabled).
   - **Publish to Windows Store**: Checkbox (default: enabled).
4. Click **"Run workflow"**.

### Required Repository Secrets:
Configure these under **Settings > Secrets and variables > Actions**:

| Secret | Purpose | Source |
|---|---|---|
| `WINGET_TOKEN` | Submits manifest pull requests to `microsoft/winget-pkgs` | GitHub Personal Access Token (classic with `public_repo` scope) |
| `MS_STORE_TENANT_ID` | Azure AD / Entra ID Tenant ID | Azure Portal / Entra ID Overview |
| `MS_STORE_SELLER_ID` | Partner Center Publisher / Seller ID | Partner Center Account Settings > Identifiers |
| `MS_STORE_CLIENT_ID` | Azure AD App Registration (Client ID) | Entra ID App Registrations |
| `MS_STORE_CLIENT_SECRET` | Azure AD App Registration Secret | Entra ID App Registrations > Certificates & secrets |
| `MS_STORE_APP_ID` | Product ID in Partner Center | Partner Center Product Overview |

---

## 📄 License

Distributed under the [MIT License](LICENSE). Copyright © 2026 Silviu Vlasceanu.
