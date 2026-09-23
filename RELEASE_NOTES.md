What's New in Luminark v1.1.12:

• External Monitor Brightness in Single-Display Mode:
  - Fixed brightness control on laptops when using an external monitor as the sole active display ("Second screen only" or clamshell mode with laptop lid closed).
  - Added low-level VESA MCCS VCP 0x10 fallback for GPU drivers (Intel Iris Xe, NVIDIA Optimus, AMD) that reject high-level brightness commands when driving a single display.
  - Automatic physical handle re-acquisition recovers instantly from display topology changes or power events.
  - Persistent PnP hardware IDs eliminate duplicate inactive monitor entries caused by dynamic GDI display renumbering.

• Screen Off & Keep-Awake on Lock:
  - "Lock & Screen Off" action and global hotkey (Win+J) natively powers down displays without sleeping the PC.
  - S0 Modern Standby laptops stay 100% awake with network and background tasks running.
  - Screen wakes at full normal brightness upon touching keyboard or mouse.

• External Monitor Video Input Switching:
  - Switch monitor inputs (DisplayPort, HDMI, USB-C) directly from the flyout or Main Window via DDC/CI.
  - Assign custom friendly names and filter visible ports with active signal indicators (✔).
