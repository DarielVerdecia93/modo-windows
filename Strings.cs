using System.Globalization;

namespace ModoWindows;

/// <summary>
/// Textos de la interfaz en el idioma de Windows (idioma de visualización del usuario).
/// Si el idioma no está traducido se usa el inglés.
/// </summary>
internal static class Strings
{
    private sealed record Texts(string SwitchToDark, string SwitchToLight, string AlwaysOnTop, string Exit, string StartWithWindows = "Start with Windows");

    private static readonly Dictionary<string, Texts> s_translations = new()
    {
        ["en"] = new("Switch to dark mode", "Switch to light mode", "Always on top", "Exit"),
        ["es"] = new("Cambiar a modo oscuro", "Cambiar a modo claro", "Superponer a las ventanas", "Salir", "Iniciar con Windows"),
        ["pt"] = new("Mudar para modo escuro", "Mudar para modo claro", "Sobrepor às janelas", "Sair"),
        ["fr"] = new("Passer en mode sombre", "Passer en mode clair", "Toujours au premier plan", "Quitter"),
        ["de"] = new("Zum dunklen Modus wechseln", "Zum hellen Modus wechseln", "Immer im Vordergrund", "Beenden"),
        ["it"] = new("Passa alla modalità scura", "Passa alla modalità chiara", "Sempre in primo piano", "Esci"),
        ["ca"] = new("Canvia al mode fosc", "Canvia al mode clar", "Superposa a les finestres", "Surt"),
    };

    // CurrentUICulture sale de GetUserDefaultUILanguage: el idioma de Windows, no el formato regional.
    private static readonly Texts s_current =
        s_translations.GetValueOrDefault(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? s_translations["en"];

    public static string SwitchToDark => s_current.SwitchToDark;
    public static string SwitchToLight => s_current.SwitchToLight;
    public static string AlwaysOnTop => s_current.AlwaysOnTop;
    public static string StartWithWindows => s_current.StartWithWindows;
    public static string Exit => s_current.Exit;
}
