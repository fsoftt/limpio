using System.Text.RegularExpressions;

namespace Limpio.Core;

/// <summary>
/// Hard-coded list of targets the engine never touches, regardless of what the catalog says.
/// Names are matched case-insensitively; '*' is a wildcard.
/// </summary>
public static class ProtectedList
{
    public static readonly IReadOnlyList<string> Packages = new[]
    {
        "Microsoft.WindowsStore", "Microsoft.StorePurchaseApp", "Microsoft.Edge*", "Microsoft.MicrosoftEdge*",
        "Microsoft.WebView2*", "Microsoft.DesktopAppInstaller", "Microsoft.Winget*",
        "*VideoExtension*", "*ImageExtension*", "*WebMediaExtensions*", "*WebpImageExtension*",
        "Microsoft.WindowsCalculator", "Microsoft.Windows.Photos", "Microsoft.Paint", "Microsoft.WindowsNotepad",
        "Microsoft.WindowsTerminal*", "Microsoft.ScreenSketch", "Microsoft.SecHealthUI",
        "*Realtek*", "*DTS*", "*Dolby*", "*Intel*", "*NVIDIA*", "*AMD*", "*Waves*",
        "Microsoft.VCLibs*", "Microsoft.NET.*", "Microsoft.UI.Xaml*", "Microsoft.WindowsAppRuntime*",
    };

    public static readonly IReadOnlyList<string> Services = new[]
    {
        "wuauserv", "UsoSvc", "BITS", "WinDefend", "WdNisSvc", "Sense", "SecurityHealthService",
        "mpssvc", "BFE", "RpcSs", "EventLog", "Winmgmt", "TrustedInstaller", "WaaSMedicSvc",
    };

    public static bool IsProtectedPackage(string name) =>
        Packages.Any(p => WildcardMatch(p, name));

    public static bool IsProtectedService(string name) =>
        Services.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Case-insensitive wildcard match where '*' matches any run of characters.</summary>
    public static bool WildcardMatch(string pattern, string text)
    {
        var rx = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
        return Regex.IsMatch(text, rx, RegexOptions.IgnoreCase);
    }
}
