using System;
using Microsoft.Win32;

namespace Lumina.Services
{
    public class ScreenTintService
    {
        private const string ScreenTintKeyPath = @"Software\Microsoft\ScreenTint";
        private const string ATConfigKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Accessibility\ATConfig\screentint";
        private const string AccessibilityKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Accessibility";

        public bool IsSupported()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ScreenTintKeyPath);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        public bool IsScreenTintActive()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ScreenTintKeyPath);
                if (key?.GetValue("Active") is int val)
                {
                    return val == 1;
                }
            }
            catch { }
            return false;
        }

        public bool SetScreenTint(bool enable)
        {
            try
            {
                int val = enable ? 1 : 0;

                // 1. Update HKCU\Software\Microsoft\ScreenTint
                using (var key1 = Registry.CurrentUser.CreateSubKey(ScreenTintKeyPath))
                {
                    key1?.SetValue("Active", val, RegistryValueKind.DWord);
                }

                // 2. Update HKCU\Software\Microsoft\Windows NT\CurrentVersion\Accessibility\ATConfig\screentint
                using (var key2 = Registry.CurrentUser.CreateSubKey(ATConfigKeyPath))
                {
                    key2?.SetValue("Active", val, RegistryValueKind.DWord);
                }

                // 3. Update HKCU\Software\Microsoft\Windows NT\CurrentVersion\Accessibility Configuration string
                using (var key3 = Registry.CurrentUser.OpenSubKey(AccessibilityKeyPath, writable: true))
                {
                    if (key3 != null)
                    {
                        string currentConfig = (key3.GetValue("Configuration") as string) ?? string.Empty;
                        if (enable && !currentConfig.Contains("screentint"))
                        {
                            string newConfig = string.IsNullOrWhiteSpace(currentConfig) ? "screentint" : $"{currentConfig};screentint";
                            key3.SetValue("Configuration", newConfig, RegistryValueKind.String);
                        }
                        else if (!enable && currentConfig.Contains("screentint"))
                        {
                            string newConfig = currentConfig.Replace("screentint", "").Replace(";;", ";").Trim(';');
                            key3.SetValue("Configuration", newConfig, RegistryValueKind.String);
                        }
                    }
                }

                // Broadcast WM_SETTINGCHANGE so shell accessibility host updates immediately
                NativeMethods.PostMessage(
                    new IntPtr(NativeMethods.HWND_BROADCAST),
                    NativeMethods.WM_SETTINGCHANGE,
                    IntPtr.Zero,
                    IntPtr.Zero);

                return true;
            }
            catch (Exception ex)
            {
                App.Log($"[ScreenTintService ERROR] {ex.Message}");
                return false;
            }
        }
    }
}
