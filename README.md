[**Website & downloads ↗**](https://silviuk.github.io/Luminark/) · [Latest release](https://github.com/silviuk/Luminark/releases/latest)

[![Support on Ko-fi](docs/assets/ko-fi.svg)](https://ko-fi.com/silviuvlasceanu)

# Luminark

Bring monitor brightness, Windows themes, and day-to-night automation together in one tray app. Set each display just right, then let Luminark handle the transitions.

**Windows 11 · Display & theme control**

## Why Luminark?

- **One slider, or one per screen.** Adjust all displays together or fine-tune each monitor, with quick brightness presets.
- **Follow your day.** Schedule light and dark themes and brightness targets, or follow Windows Night Light status.
- **Stay in your flow.** Scroll over the tray icon for brightness. Use the flyout for supported monitor inputs, volume, sleep prevention, and screen-off controls.

## See it in action

<img src="docs/assets/dashboard.png" alt="Brightness and monitor controls in the dark-mode dashboard." width="700">

*Brightness and monitor controls in the dark-mode dashboard.*

<img src="docs/assets/automation.png" alt="Schedule theme changes or follow Windows Night Light." width="700">

*Schedule theme changes or follow Windows Night Light.*

## Get started

1. Download the latest Windows installer from GitHub Releases and launch Luminark.
2. Open the tray flyout and check that your displays are detected. Enable DDC/CI in your monitor menu if needed.
3. Set your brightness levels and choose a schedule or Windows Night Light synchronization.

### Tray shortcuts

| Gesture | Action |
| --- | --- |
| Click | Open quick controls |
| Double-click | Toggle light / dark theme |
| Scroll over icon | Adjust master brightness by 5% |
| Right-click | Open the tray menu |

## Requirements & limits

Windows 11. External monitor brightness, input, and speaker controls depend on the monitor’s DDC/CI capabilities; built-in laptop brightness depends on WMI support. Source builds use the .NET SDK pinned by global.json.

Available controls vary by display and connection. Some monitors, docks, and adapters do not expose every DDC/CI function.

## Documentation & contributing

See [development, validation, and release instructions](docs/DEVELOPMENT.md).

Found a problem or have an idea? [Open an issue](https://github.com/silviuk/Luminark/issues). If the app helps you, [support its development on Ko-fi](https://ko-fi.com/silviuvlasceanu).

© 2026 Silviu Vlasceanu. [License](LICENSE).
