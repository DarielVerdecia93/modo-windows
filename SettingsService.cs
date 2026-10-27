using Microsoft.Win32;

namespace ModoWindows;

/// <summary>
/// Preferencias de la app, guardadas en HKCU\Software\ModoWindows.
/// </summary>
internal static class SettingsService
{
    private const string AppKey = @"Software\ModoWindows";
    private const string AlwaysOnTopValue = "AlwaysOnTop";

    // Por defecto la burbuja se superpone a las ventanas.
    public static bool AlwaysOnTop
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(AppKey);
            return key?.GetValue(AlwaysOnTopValue) is not int value || value != 0;
        }
        set
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(AppKey, writable: true);
            key.SetValue(AlwaysOnTopValue, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
