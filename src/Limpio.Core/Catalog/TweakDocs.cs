using System.Text;
using Limpio.Core.Models;

namespace Limpio.Core.Catalog;

/// <summary>Builds the markdown table listed in docs/tweaks.md.</summary>
public static class TweakDocs
{
    public static string ToMarkdown(TweakCatalog catalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Tweak catalog");
        sb.AppendLine();
        sb.AppendLine("Generated from `src/Limpio.Core/Catalog/tweaks.json`. Do not edit by hand: run");
        sb.AppendLine("`LIMPIO_UPDATE_DOCS=1 dotnet test` to regenerate it.");
        sb.AppendLine();
        sb.AppendLine($"{catalog.Tweaks.Count} tweaks.");
        foreach (var g in catalog.Tweaks.GroupBy(t => t.Category).OrderBy(g => g.Key))
        {
            sb.AppendLine();
            sb.AppendLine($"## {g.Key}");
            sb.AppendLine();
            sb.AppendLine("| Id | Title | Risk | Presets | Targets |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var t in g)
            {
                var presets = t.Presets.Count == 0 ? "custom only" : string.Join(", ", t.Presets.Select(p => p.ToString().ToLowerInvariant()));
                var targets = string.Join("<br>", t.Actions.Select(a => $"{a.Kind}: `{Escape(a.Target)}`"));
                sb.AppendLine($"| `{t.Id}` | {Escape(t.Title.En)} | {t.Risk.ToString().ToLowerInvariant()} | {presets} | {targets} |");
            }
        }
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("|", @"\|");
}
