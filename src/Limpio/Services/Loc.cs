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

    /// <summary>Singular/plural: looks up key.one when n is 1, key.other otherwise ({0} is n).</summary>
    public static string P(string key, int n) => F(key + (n == 1 ? ".one" : ".other"), n);

    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
}
