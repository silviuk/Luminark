using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Lumina.ViewModels;

namespace Lumina.Services
{
    public class HotkeyService : IDisposable
    {
        public const int HOTKEY_ID_DARK = 9001;
        public const int HOTKEY_ID_LIGHT = 9002;
        public const int HOTKEY_ID_TOGGLE = 9003;

        private IntPtr _hwnd;
        private readonly MainViewModel _viewModel;
        private readonly HashSet<int> _registeredIds = new();

        public HotkeyService(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void Initialize(IntPtr hwnd)
        {
            _hwnd = hwnd;
            RegisterAll();
        }

        public void RegisterAll()
        {
            UnregisterAll();

            if (_hwnd == IntPtr.Zero || !_viewModel.EnableGlobalShortcuts)
            {
                return;
            }

            RegisterSingle(HOTKEY_ID_DARK, _viewModel.DarkModeShortcut, "Dark Mode");
            RegisterSingle(HOTKEY_ID_LIGHT, _viewModel.LightModeShortcut, "Light Mode");
            RegisterSingle(HOTKEY_ID_TOGGLE, _viewModel.ToggleThemeShortcut, "Toggle Theme");
        }

        private void RegisterSingle(int id, string? shortcut, string description)
        {
            if (string.IsNullOrWhiteSpace(shortcut) || shortcut.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (TryParseShortcut(shortcut, out uint modifiers, out uint vk))
            {
                // MOD_NOREPEAT (0x4000) prevents continuous repeated hotkey triggering when held down
                bool success = NativeMethods.RegisterHotKey(_hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, vk);
                if (success)
                {
                    _registeredIds.Add(id);
                    App.Log($"[HotkeyService] Registered global shortcut for {description}: {shortcut} (id={id})");
                }
                else
                {
                    int err = Marshal.GetLastWin32Error();
                    App.Log($"[HotkeyService] Failed to register global shortcut for {description}: {shortcut} (error={err})");
                }
            }
            else
            {
                App.Log($"[HotkeyService] Could not parse shortcut '{shortcut}' for {description}");
            }
        }

        public void UnregisterAll()
        {
            if (_hwnd == IntPtr.Zero) return;

            foreach (int id in _registeredIds)
            {
                NativeMethods.UnregisterHotKey(_hwnd, id);
            }
            _registeredIds.Clear();
        }

        public bool HandleHotkeyMessage(int id)
        {
            switch (id)
            {
                case HOTKEY_ID_DARK:
                    App.Log("[HotkeyService] Hotkey triggered: Dark Mode");
                    _viewModel.SetDarkMode();
                    return true;

                case HOTKEY_ID_LIGHT:
                    App.Log("[HotkeyService] Hotkey triggered: Light Mode");
                    _viewModel.SetLightMode();
                    return true;

                case HOTKEY_ID_TOGGLE:
                    App.Log("[HotkeyService] Hotkey triggered: Toggle Theme");
                    _viewModel.ToggleTheme();
                    return true;
            }

            return false;
        }

        public static bool TryParseShortcut(string shortcut, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;

            if (string.IsNullOrWhiteSpace(shortcut)) return false;

            var parts = shortcut.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string? keyPart = null;

            foreach (var p in parts)
            {
                var lower = p.Trim().ToLowerInvariant();
                if (lower is "ctrl" or "control")
                    modifiers |= NativeMethods.MOD_CONTROL;
                else if (lower is "alt")
                    modifiers |= NativeMethods.MOD_ALT;
                else if (lower is "shift")
                    modifiers |= NativeMethods.MOD_SHIFT;
                else if (lower is "win" or "windows")
                    modifiers |= NativeMethods.MOD_WIN;
                else
                    keyPart = p.Trim();
            }

            if (string.IsNullOrEmpty(keyPart)) return false;

            // Attempt to parse standard WPF Key
            if (Enum.TryParse<Key>(keyPart, true, out var key))
            {
                int virtualKey = KeyInterop.VirtualKeyFromKey(key);
                if (virtualKey > 0)
                {
                    vk = (uint)virtualKey;
                    return true;
                }
            }

            // Single character fallback (e.g. 'D', 'L', '0', etc.)
            if (keyPart.Length == 1)
            {
                char c = char.ToUpperInvariant(keyPart[0]);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    vk = (uint)c;
                    return true;
                }
            }

            // Function keys fallback (F1 - F24)
            if (keyPart.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(keyPart.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
            {
                vk = (uint)(0x70 + (fNum - 1)); // VK_F1 is 0x70
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            UnregisterAll();
        }
    }
}
