using Microsoft.Win32;

namespace ModoWindows;

/// <summary>
/// Lee y cambia el tema claro/oscuro de Windows (el mismo ajuste que
/// Configuración &gt; Personalización &gt; Colores).
/// </summary>
internal static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsValue = "AppsUseLightTheme";
    private const string SystemValue = "SystemUsesLightTheme";

    public static bool IsLightTheme()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(AppsValue) is not int value || value != 0;
    }

    public static void SetLightTheme(bool light)
    {
        int value = light ? 1 : 0;
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PersonalizeKey, writable: true))
        {
            key.SetValue(AppsValue, value, RegistryValueKind.DWord);
            key.SetValue(SystemValue, value, RegistryValueKind.DWord);
        }

        // Avisa a Explorer y al resto de apps para que se actualicen al momento.
        // Se hace en segundo plano porque el broadcast puede tardar si alguna ventana no responde.
        _ = Task.Run(() => NativeMethods.SendMessageTimeout(
            NativeMethods.HWND_BROADCAST,
            NativeMethods.WM_SETTINGCHANGE,
            0,
            "ImmersiveColorSet",
            NativeMethods.SMTO_ABORTIFHUNG,
            500,
            out _));
    }
}
