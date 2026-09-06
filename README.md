# Lumina ☀️🌙

> **Dynamic Windows 11 Theme, Night Light & Multi-Monitor Hardware Brightness Controller**

Lumina is a native Windows 11 utility engineered to automatically harmonize your display environment. It synchronizes Windows system and app themes with day/night schedules or Windows Night Light, and provides seamless hardware-level multi-monitor brightness control via **DDC/CI** and **WMI**.

![Windows 11](https://img.shields.io/badge/Platform-Windows%2011-0078D4?logo=windows11)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Release](https://img.shields.io/badge/Release-v1.0.0-orange)

---

## ✨ Features

- 🌓 **Automated Dark & Light Mode**: Switches Windows shell and applications automatically based on custom times, sunset/sunrise, or Windows Night Light live state.
- 💡 **Multi-Monitor DDC/CI Control**: Hardware brightness control for external monitors via DDC/CI (I2C bus) and laptop internal panels via WMI.
- 🎛️ **Master & Individual Sliders**: Adjust all screens simultaneously with a Master Slider or fine-tune individual monitors with quick presets (25%, 50%, 75%, 100%).
- 🌅 **Windows Night Light Sync**:
  - *Follow Live State*: Automatically switches to Dark Mode and dims brightness when Night Light engages.
  - *Follow Sunset & Sunrise*: Syncs with Windows 11 location-calculated sunset and sunrise times.
  - *Drive Night Light*: Controls Windows Night Light state directly.
- ⚡ **Ultra-Low Resource Footprint**:
  - **~7 MB RAM** working set when minimized to system tray.
  - **0.0% CPU** at rest with event-driven scheduling.
- 🎨 **Modern Windows 11 Fluent Design**: Built with WPF-UI, Windows 11 typography, rounded corners, snap layout support, and smooth sliders.
- 🔔 **System Tray Integration**: Lives in the notification area with a quick context menu for immediate theme toggling and brightness adjustment.

---

## 📥 Installation

### Option 1: WinGet (Recommended)
```powershell
winget install silviuk.Lumina
```

### Option 2: Setup Installer (.exe)
Download the latest `Lumina-Setup-v1.0.0.exe` from [GitHub Releases](https://github.com/silviuk/Lumina/releases/latest) and run the installer.

### Option 3: Build from Source
```powershell
git clone https://github.com/silviuk/Lumina.git
cd Lumina
dotnet build -c Release
dotnet run -c Release
```

---

## 🚀 Usage

1. **Quick Switch**: Click the theme toggle button in the header or tray icon to immediately switch between Dark and Light mode.
2. **Brightness**: Use the **Master Brightness** slider to adjust all displays together, or use individual sliders for each screen.
3. **Schedule**: Enable **Automatic Theme Schedule** under the *Schedule & Automation* tab to set daily Day and Night transition times.
4. **Night Light**: Enable **Sync with Windows Night Light** to link your theme and brightness transitions directly with Windows Night Light.
5. **Autostart**: Toggle **Launch at Windows Startup** under *Settings* to run quietly in the system tray on boot.

---

## 🛠️ Architecture & Tech Stack

- **Framework**: .NET 8 WPF with C# 12
- **UI & Styling**: [WPF-UI](https://github.com/lepoco/wpfui) (Windows 11 Fluent Design)
- **Hardware Interop**:
  - `dxva2.dll` (`GetMonitorBrightness`, `SetMonitorBrightness`, `GetPhysicalMonitorsFromHMONITOR`)
  - `user32.dll` (`EnumDisplayMonitors`, `SendMessageTimeout`, `RegisterWindowMessage`)
  - `root\wmi` (`WmiMonitorBrightnessMethods`)
- **Packaging**: Inno Setup with silent install support (`/VERYSILENT`)

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
