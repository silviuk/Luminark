What's New in Luminark v1.1.20:

- Fixed rapid brightness key presses being ignored and appearing together on a later press, including Logitech keyboard brightness controls.
- Hardware brightness changes now update the display state without writing the same value back to the laptop.
- Removed the 1.2-second event suppression window so valid brightness changes are processed immediately.
- Fixed a cancellation-token race that could drop monitor brightness updates.
