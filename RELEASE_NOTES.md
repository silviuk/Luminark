What's New in Luminark v1.1.17:

• Reliable "Lock & Screen Off" for Laptop & External Displays:
  - Fixed Per-Thread Keep-Awake Leak: Ensured Win32 SetThreadExecutionState calls always execute on the main UI thread rather than background ThreadPool threads, preventing orphaned ES_DISPLAY_REQUIRED kernel holds from blocking display turn-off when locking with Prevent Sleep enabled.
  - Persistent Lock Display Timeout: Removed the premature 5-second timeout reset that cancelled screen power-off while Windows 11 LogonUI and Windows Hello were still initializing. Both Console Lock (VIDEOCONLOCK) and Video Idle (VIDEOIDLE) timeouts now remain at 1s while locked and restore automatically upon unlock, exit, or startup.
  - Multi-Wave External Monitor Standby: Sequenced DDC/CI power-off commands (both VCP 0xD6 0x02 Standby and 0x04 Off with physical handle re-acquisition) and desktop SC_MONITORPOWER broadcasts after LockWorkStation settles so lock screen rendering never wakes external monitors back up.
  - Lock-Aware Device Change Filtering: Suppressed background WM_DEVICECHANGE / I2C monitor scans while the workstation is locked so sleeping monitors are never awakened by background PnP events.
