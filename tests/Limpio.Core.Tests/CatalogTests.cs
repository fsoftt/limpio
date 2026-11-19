using Limpio.Core;
using Limpio.Core.Actions;
using Limpio.Core.Catalog;
using Limpio.Core.Models;

namespace Limpio.Core.Tests;

public class CatalogTests
{
    private static readonly TweakCatalog Catalog = TweakCatalog.LoadEmbedded();

    [Fact]
    public void Embedded_catalog_parses_and_is_not_empty()
    {
        Assert.True(Catalog.Tweaks.Count > 40);
        Assert.Contains(Catalog.Tweaks, t => t.Actions.OfType<AppxAction>().Any());
        Assert.Contains(Catalog.Tweaks, t => t.Actions.OfType<ServiceAction>().Any());
        Assert.Contains(Catalog.Tweaks, t => t.Actions.OfType<ScheduledTaskAction>().Any());
        Assert.Contains(Catalog.Tweaks, t => t.Actions.OfType<RegistryAction>().Any());
    }

    [Fact]
    public void Embedded_catalog_is_consistent()
    {
        var errors = Catalog.Validate();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void Ids_are_unique()
    {
        var dup = Catalog.Tweaks.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(dup);
    }

    [Fact]
    public void Every_category_is_used()
    {
        foreach (var c in Enum.GetValues<Category>())
            Assert.Contains(Catalog.Tweaks, t => t.Category == c);
    }

    [Fact]
    public void No_catalog_action_targets_a_protected_item()
    {
        foreach (var t in Catalog.Tweaks)
            foreach (var a in t.Actions)
                Assert.False(a.IsProtected, $"{t.Id} -> {a.Target}");
    }

    [Fact]
    public void Parse_reads_all_action_types_from_json()
    {
        const string json = """
        { "version": 1, "tweaks": [ {
          "id": "x", "category": "apps", "risk": "safe", "presets": ["recommended","aggressive"],
          "title": {"es":"a","en":"b"}, "description": {"es":"a","en":"b"},
          "actions": [
            {"type":"appx","packageName":"Foo.Bar"},
            {"type":"service","name":"Svc","startType":"Manual"},
            {"type":"scheduledTask","path":"\\A\\B"},
            {"type":"registry","hive":"HKCU","path":"Software\\X","name":"V","valueType":"dword","value":1}
          ] } ] }
        """;
        var c = TweakCatalog.Parse(json);
        var t = Assert.Single(c.Tweaks);
        Assert.Equal(4, t.Actions.Count);
        Assert.Equal(Risk.Safe, t.Risk);
        Assert.Equal(Category.Apps, t.Category);
        Assert.Empty(c.Validate());
    }

    [Fact]
    public void Validate_flags_duplicates_missing_text_and_protected_targets()
    {
        const string json = """
        { "tweaks": [
          { "id": "d", "category": "apps", "risk": "safe", "presets": ["aggressive"],
            "title": {"es":"a","en":"b"}, "description": {"es":"a","en":"b"},
            "actions": [ {"type":"appx","packageName":"Microsoft.WindowsStore"} ] },
          { "id": "d", "category": "services", "risk": "safe", "presets": ["aggressive"],
            "title": {"es":"","en":"b"}, "description": {"es":"a","en":"b"},
            "actions": [ {"type":"service","name":"wuauserv","startType":"Disabled"} ] }
        ] }
        """;
        var errors = TweakCatalog.Parse(json).Validate();
        Assert.Contains(errors, e => e.Contains("Duplicate id"));
        Assert.Contains(errors, e => e.Contains("title needs"));
        Assert.Equal(2, errors.Count(e => e.Contains("protected")));
    }

    [Fact]
    public void Unknown_category_fails_to_parse()
    {
        const string json = """{ "tweaks": [ { "id":"x", "category":"nope" } ] }""";
        Assert.ThrowsAny<Exception>(() => TweakCatalog.Parse(json));
    }

    [Fact]
    public void Presets_are_nested()
    {
        var min = PresetSelector.Select(Catalog, Preset.Minimal);
        var rec = PresetSelector.Select(Catalog, Preset.Recommended);
        var agg = PresetSelector.Select(Catalog, Preset.Aggressive);
        Assert.NotEmpty(min);
        Assert.True(min.IsSubsetOf(rec));
        Assert.True(rec.IsSubsetOf(agg));
        Assert.True(min.Count < rec.Count && rec.Count < agg.Count);
        Assert.Empty(PresetSelector.Select(Catalog, Preset.Custom));
    }

    [Fact]
    public void Recommended_never_contains_advanced_tweaks()
    {
        var rec = PresetSelector.Select(Catalog, Preset.Recommended);
        Assert.DoesNotContain(Catalog.Tweaks, t => t.Risk == Risk.Advanced && rec.Contains(t.Id));
    }

    [Fact]
    public void Localized_text_falls_back_and_follows_language()
    {
        var t = new LocalizedText { Es = "hola", En = "hello" };
        Assert.Equal("hola", t.Get("es-CO"));
        Assert.Equal("hello", t.Get("en-US"));
        Assert.Equal("hola", new LocalizedText { Es = "hola" }.Get("en"));
    }
}
