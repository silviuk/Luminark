What's New in Luminark v1.1.18:

• Fixed External USB-C & 4K Monitor Wake After "Lock & Screen Off":
  - Prevented Monitor Controller Lockups: Removed raw DDC/CI VCP 0xD6 (0x04 Soft Power Off) waves during lock that caused external USB-C / DisplayPort monitors (including 42" LG 4K displays) to turn off their internal scaler/USB-C controller and require unplugging from AC mains. External monitors now enter native DisplayPort/HDMI standby cleanly when Windows drops the video link and wake automatically when the laptop wakes.
  - Full Lock-Screen Wake Timeout for USB-C Link Training: Automatically restores Console Lock (VIDEOCONLOCK) and Video Idle (VIDEOIDLE) timeouts to their normal values as soon as displays finish powering down on lock, ensuring slow-waking 4K/USB-C monitors have the full 60+ seconds to complete link training when waking on the lock screen.
  - Self-Healing Lock State Detection: Automatically verifies OS session lock state on display wake and plug/unplug events so Windows Automatic Restart Sign-On (ARSO) startups never defer monitor detection.
