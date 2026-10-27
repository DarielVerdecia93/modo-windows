using Microsoft.Win32;

namespace ModoWindows;

/// <summary>
/// Preferencias de la app, guardadas en HKCU\Software\ModoWindows.
/// </summary>
internal static class SettingsService
{
    private const string AppKey = @"Software\ModoWindows";
    private const string AlwaysOnTopValue = "AlwaysOnTop";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "ModoWindows";

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

    public static bool StartWithWindows
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string command && !string.IsNullOrWhiteSpace(command);
        }
        set
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (value)
            {
                string? executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    throw new InvalidOperationException("No se pudo encontrar el ejecutable de Modo Windows.");
                }

                key.SetValue(RunValue, $"\"{executablePath}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(RunValue, throwOnMissingValue: false);
            }
        }
    }
}
