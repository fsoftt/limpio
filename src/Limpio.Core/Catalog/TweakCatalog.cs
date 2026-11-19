using System.Text.Json;
using System.Text.Json.Serialization;
using Limpio.Core.Actions;
using Limpio.Core.Backup;
using Limpio.Core.Models;

namespace Limpio.Core.Catalog;

public sealed class TweakCatalog
{
    public int Version { get; set; } = 1;
    public List<Tweak> Tweaks { get; set; } = new();

    public Tweak? Find(string id) => Tweaks.FirstOrDefault(t => t.Id == id);

    public static TweakCatalog Parse(string json)
    {
        var options = new JsonSerializerOptions(Json.Options);
        return JsonSerializer.Deserialize<TweakCatalog>(json, options)
               ?? throw new JsonException("Empty catalog");
    }

    public static TweakCatalog LoadEmbedded()
    {
        var asm = typeof(TweakCatalog).Assembly;
        using var s = asm.GetManifestResourceStream("Limpio.Core.tweaks.json")
                      ?? throw new InvalidOperationException("tweaks.json not embedded");
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }

    /// <summary>Returns a list of problems; empty when the catalog is consistent.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var seen = new HashSet<string>();
        foreach (var t in Tweaks)
        {
            if (string.IsNullOrWhiteSpace(t.Id)) errors.Add("Tweak without id");
            else if (!seen.Add(t.Id)) errors.Add($"Duplicate id: {t.Id}");
            if (string.IsNullOrWhiteSpace(t.Title.Es) || string.IsNullOrWhiteSpace(t.Title.En))
                errors.Add($"{t.Id}: title needs es and en");
            if (string.IsNullOrWhiteSpace(t.Description.Es) || string.IsNullOrWhiteSpace(t.Description.En))
                errors.Add($"{t.Id}: description needs es and en");
            if (!Enum.IsDefined(t.Category)) errors.Add($"{t.Id}: invalid category");
            if (t.Actions.Count == 0) errors.Add($"{t.Id}: no actions");
            if (t.Presets.Contains(Preset.Custom)) errors.Add($"{t.Id}: custom is not a valid preset in the catalog");
            if (t.Presets.Contains(Preset.Minimal) && !t.Presets.Contains(Preset.Recommended))
                errors.Add($"{t.Id}: minimal must also be in recommended");
            if (t.Presets.Contains(Preset.Recommended) && !t.Presets.Contains(Preset.Aggressive))
                errors.Add($"{t.Id}: recommended must also be in aggressive");
            if (t.Risk == Risk.Advanced && t.Presets.Contains(Preset.Recommended))
                errors.Add($"{t.Id}: advanced tweaks cannot be in the recommended preset");
            foreach (var a in t.Actions)
            {
                if (a.IsProtected) errors.Add($"{t.Id}: action targets protected item {a.Target}");
                var emptyTarget = a switch
                {
                    AppxAction x => string.IsNullOrWhiteSpace(x.PackageName),
                    ServiceAction x => string.IsNullOrWhiteSpace(x.Name),
                    ScheduledTaskAction x => string.IsNullOrWhiteSpace(x.Path),
                    RegistryAction x => string.IsNullOrWhiteSpace(x.Path) || string.IsNullOrWhiteSpace(x.Name),
                    Win32UninstallAction x => string.IsNullOrWhiteSpace(x.DisplayName) || x.Uninstallers.Count == 0,
                    _ => false,
                };
                if (emptyTarget) errors.Add($"{t.Id}: action with empty target");
                if (a is RegistryAction r)
                {
                    if (r.Hive is not ("HKLM" or "HKCU")) errors.Add($"{t.Id}: hive must be HKLM or HKCU");
                    if (r.ValueType is not ("dword" or "qword" or "string")) errors.Add($"{t.Id}: bad valueType {r.ValueType}");
                }
                if (a is ServiceAction sv && !Enum.TryParse<Platform.StartType>(sv.StartType, true, out _))
                    errors.Add($"{t.Id}: bad startType {sv.StartType}");
            }
        }
        return errors;
    }
}

public static class PresetSelector
{
    /// <summary>Ids of the tweaks that belong to a preset. Custom returns an empty set.</summary>
    public static HashSet<string> Select(TweakCatalog catalog, Preset preset) =>
        preset == Preset.Custom
            ? new HashSet<string>()
            : catalog.Tweaks.Where(t => t.IsInPreset(preset)).Select(t => t.Id).ToHashSet();
}
