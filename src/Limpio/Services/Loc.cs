using System.Globalization;

namespace Limpio.Services;

/// <summary>Minimal localization: Spanish (default) and English, chosen from the system UI culture.</summary>
public sealed class Loc
{
    public static Loc Instance { get; } = new();

    public string Language { get; private set; } = "es";
    public bool IsSpanish => Language == "es";

    public static void Init(string? forced)
    {
        var lang = forced ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Instance.Language = lang.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";
    }

    public string this[string key] => T(key);

    public static string T(string key)
    {
        var d = Instance.IsSpanish ? Strings.Es : Strings.En;
        if (d.TryGetValue(key, out var v)) return v;
        return Strings.Es.TryGetValue(key, out var f) ? f : key;
    }

    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
}
