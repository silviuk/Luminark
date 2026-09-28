What's New in Luminark v1.1.13:

• Memory & Performance Optimization:
  - Eliminated artificial working set ballooning and forced Gen2 GC cycles, establishing smooth and stable memory utilization.
  - Resolved an unmanaged handle and background thread leak in SettingsService.
  - Fixed WMI COM object leaks by deterministically disposing all ManagementObject query instances.
  - Cleaned up static SystemEvents event subscriptions in ThemeService to prevent memory leaks on exit.
  - Properly released native Win32 GDI icon handles on exit.

• Background Hook & Polling Efficiency:
  - Refactored TrayScrollHook: Eliminated global mouse movement processing across Windows, saving millions of marshal operations and reducing background CPU usage to 0%.
  - Refactored NightLightService into a purely event-driven model without polling timeouts, preventing unnecessary CPU and thread wakeups.
  - Consolidated Keep-Awake timers, pausing high-frequency ticks when sleep prevention is set to Forever.
  - Increased schedule evaluation interval to 30s for minimal idle impact.

• Security & Reliability Enhancements:
  - Added startup safety recovery for Windows Console Lock Display Timeout (VIDEOCONLOCK) to ensure lock screen timeout is never stuck at 1s after an abnormal termination.
  - Added log file rotation capped at 5 MB in App.Log to prevent unbounded disk usage.
