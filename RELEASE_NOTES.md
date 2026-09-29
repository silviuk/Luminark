What's New in Luminark v1.1.16:

• Mouse Responsiveness & Jitter Elimination:
  - Isolated Tray Mouse Hook: Moved the system tray scroll hook to a dedicated, high-priority background thread with its own message pump. Mouse hook callbacks now return instantly in nanoseconds, completely eliminating cursor stutter or jitter during normal PC usage.
  - Asynchronous Hardware I2C Queries: Offloaded physical monitor enumeration and DDC/CI queries to background tasks so hardware display scans during USB/device changes never block the UI thread.
  - Removed Synthetic Mouse Events: Eliminated zero-delta mouse nudge events from the keep-awake service, ensuring smooth, uninterrupted cursor tracking for high-polling-rate mice.
  - Tray Scroll Toggle Setting: Added a toggle switch in Settings to enable or disable mouse wheel brightness adjustments over the system tray icon.
