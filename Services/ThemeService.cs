using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace Lumina.Services
{
    public class ThemeService
    {
        private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public event Action<bool>? ThemeChanged;

        public ThemeService()
        {
            try
            {
                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.Window)
                    {
                        bool isLight = IsLightTheme();
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                ApplicationThemeManager.Apply(
                                    isLight ? ApplicationTheme.Light : ApplicationTheme.Dark,
                                    Wpf.Ui.Controls.WindowBackdropType.None,
                                    updateAccent: true);
                            }
                            catch { }
                            ThemeChanged?.Invoke(isLight);
                        });
                    }
                };
            }
            catch { }
        }

        public bool IsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
                if (key != null)
                {
                    object? appsVal = key.GetValue("AppsUseLightTheme");
                    if (appsVal is int intVal)
                    {
                        return intVal == 1;
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return false;
        }

        public bool IsSystemLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
                if (key != null)
                {
                    object? sysVal = key.GetValue("SystemUsesLightTheme");
                    if (sysVal is int intVal)
                    {
                        return intVal == 1;
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return false;
        }

        public void SetTheme(bool isLight)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(PersonalizeKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("AppsUseLightTheme", isLight ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("SystemUsesLightTheme", isLight ? 1 : 0, RegistryValueKind.DWord);
                    }
                }

                // Asynchronously broadcast change to avoid blocking UI thread
                Task.Run(() =>
                {
                    try
                    {
                        NativeMethods.SendMessageTimeout(
                            (IntPtr)NativeMethods.HWND_BROADCAST,
                            NativeMethods.WM_SETTINGCHANGE,
                            UIntPtr.Zero,
                            "ImmersiveColorSet",
                            NativeMethods.SMTO_ABORTIFHUNG,
                            500,
                            out _);

                        NativeMethods.SendMessageTimeout(
                            (IntPtr)NativeMethods.HWND_BROADCAST,
                            NativeMethods.WM_SETTINGCHANGE,
                            UIntPtr.Zero,
                            "PolicyChanged",
                            NativeMethods.SMTO_ABORTIFHUNG,
                            500,
                            out _);
                    }
                    catch { }
                });

                // Update WPF-UI app theme safely
                if (System.Windows.Application.Current != null)
                {
                    if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                    {
                        try { ApplicationThemeManager.Apply(isLight ? ApplicationTheme.Light : ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: true); } catch { }
                    }
                    else
                    {
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            try { ApplicationThemeManager.Apply(isLight ? ApplicationTheme.Light : ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: true); } catch { }
                        });
                    }
                }

                ThemeChanged?.Invoke(isLight);
            }
            catch (Exception ex)
            {
                App.Log($"[ThemeService] Failed to set theme: {ex.Message}");
            }
        }
    }
}
