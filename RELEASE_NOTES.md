What's New in Luminark v1.1.14:

• Monitor Video Input Switching & Detection Fixes:
  - Fixed phantom inputs: Stopped populating unverified standard inputs (e.g. VGA, DVI, extra HDMI ports). The app now accurately respects your monitor's hardware DDC/CI capabilities (VCP 0x60) to show only physical ports.
  - Fixed input switch cancellation: Closing or clicking outside the quick controls tray flyout no longer aborts active input switches.
  - Added "Switch Now" button: Added an immediate execution button and Enter key shortcut to switch inputs instantly without waiting for the countdown timer.
  - Resilient DDC/CI communication: Added automatic handle reacquisition, retries, and high-byte preservation to ensure input switch commands reach the monitor hardware reliably.
  - Background live input synchronization: Active input status is refreshed when opening the tray flyout, remaining synchronized even when inputs are changed using the monitor's physical OSD buttons.
  - Custom input management: Added custom port code addition and input removal directly from the Main Dashboard settings.
