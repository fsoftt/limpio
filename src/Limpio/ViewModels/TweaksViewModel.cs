using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Limpio.Core.Catalog;
using Limpio.Core.Models;
using Limpio.Services;

namespace Limpio.ViewModels;

public sealed record PresetOption(Preset Preset, string Label, string Hint);

public partial class TweakItemViewModel : ObservableObject
{
    private readonly TweaksViewModel _owner;

    public Tweak Tweak { get; }
    public string Title => Tweak.Title.Get(Loc.Instance.Language);
    public string Description => Tweak.Description.Get(Loc.Instance.Language);
    public string? Note => Tweak.Note?.Get(Loc.Instance.Language);
    public bool HasNote => Tweak.Note is not null;
    public string RiskText => Loc.T("risk." + Tweak.Risk.ToString().ToLowerInvariant());
    public Brush RiskBrush => Tweak.Risk switch
    {
        Risk.Safe => Frozen(0x2E, 0x9E, 0x5B),
        Risk.Moderate => Frozen(0xD9, 0x8E, 0x04),
        _ => Frozen(0xD1, 0x34, 0x38),
    };

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(StateText), nameof(Actionable), nameof(HasState))]
    private ActionState? _state;

    [ObservableProperty] private bool _isSelected;

    public bool Actionable => State is null or ActionState.NotApplied;
    public bool HasState => State is ActionState.Applied or ActionState.NotPresent;
    public string StateText => State switch
    {
        ActionState.Applied => Loc.T("state.applied"),
        ActionState.NotPresent => Loc.T("state.notpresent"),
        _ => "",
    };

    public TweakItemViewModel(Tweak tweak, TweaksViewModel owner)
    {
        Tweak = tweak;
        _owner = owner;
    }

    partial void OnIsSelectedChanged(bool value) => _owner.OnItemToggled(this);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}

public sealed class CategoryGroupViewModel
{
    public string Title { get; }
    public string Glyph { get; }
    public ObservableCollection<TweakItemViewModel> Items { get; } = new();
    public string CountText => Items.Count.ToString();

    public CategoryGroupViewModel(Category c, IEnumerable<TweakItemViewModel> items)
    {
        Title = Loc.T("cat." + c.ToString().ToLowerInvariant());
        Glyph = c switch
        {
            Category.Apps => "Apps24", Category.Telemetry => "DataUsage24", Category.Privacy => "LockClosed24",
            Category.AI => "Bot24", Category.Ads => "Megaphone24", Category.Services => "Settings24", _ => "CalendarClock24",
        };
        foreach (var i in items) Items.Add(i);
    }
}

public partial class TweaksViewModel : ObservableObject
{
    private readonly AppState _app;
    private readonly List<TweakItemViewModel> _all;
    private bool _bulk;

    public ObservableCollection<CategoryGroupViewModel> Groups { get; } = new();
    public IReadOnlyList<PresetOption> Presets { get; }

    [ObservableProperty] private PresetOption _selectedPreset;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private int _visibleCount;

    public AppState App => _app;
    public string PresetHint => SelectedPreset.Hint;
    public IReadOnlyList<TweakItemViewModel> AllItems => _all;

    public TweaksViewModel(AppState app)
    {
        _app = app;
        _all = app.Catalog.Tweaks.Select(t => new TweakItemViewModel(t, this)).ToList();
        Presets = new[]
        {
            new PresetOption(Preset.Minimal, Loc.T("preset.minimal"), Loc.T("preset.minimal.hint")),
            new PresetOption(Preset.Recommended, Loc.T("preset.recommended"), Loc.T("preset.recommended.hint")),
            new PresetOption(Preset.Aggressive, Loc.T("preset.aggressive"), Loc.T("preset.aggressive.hint")),
            new PresetOption(Preset.Custom, Loc.T("preset.custom"), Loc.T("preset.custom.hint")),
        };
        var start = Enum.TryParse<Preset>(app.Options.Preset, true, out var p) ? p : Preset.Recommended;
        _selectedPreset = Presets.First(x => x.Preset == start);
        app.ScanCompleted += OnScanCompleted;
        BuildGroups();
        ApplyPreset(_selectedPreset.Preset);
        if (app.Options.Select.Count > 0) SelectOnly(app.Options.Select);
    }

    public IEnumerable<Tweak> SelectedTweaks => _all.Where(i => i.IsSelected && i.Actionable).Select(i => i.Tweak);

    private void OnScanCompleted()
    {
        var map = _app.States.ToDictionary(s => s.Tweak.Id, s => s.State);
        _bulk = true;
        foreach (var i in _all)
        {
            i.State = map.TryGetValue(i.Tweak.Id, out var st) ? st : null;
            if (!i.Actionable) i.IsSelected = false;
        }
        _bulk = false;
        if (SelectedPreset.Preset != Preset.Custom) ApplyPreset(SelectedPreset.Preset);
        else Recount();
        BuildGroups();
    }

    partial void OnSelectedPresetChanged(PresetOption value)
    {
        OnPropertyChanged(nameof(PresetHint));
        if (!_bulk && value.Preset != Preset.Custom) ApplyPreset(value.Preset);
    }

    partial void OnSearchChanged(string value) => BuildGroups();

    private void ApplyPreset(Preset preset)
    {
        var ids = PresetSelector.Select(_app.Catalog, preset);
        _bulk = true;
        foreach (var i in _all) i.IsSelected = i.Actionable && ids.Contains(i.Tweak.Id);
        _bulk = false;
        Recount();
    }

    private void SelectOnly(HashSet<string> ids)
    {
        _bulk = true;
        foreach (var i in _all) i.IsSelected = i.Actionable && ids.Contains(i.Tweak.Id);
        SelectedPreset = Presets.First(x => x.Preset == Preset.Custom);
        _bulk = false;
        Recount();
    }

    internal void OnItemToggled(TweakItemViewModel _)
    {
        if (_bulk) return;
        _bulk = true;
        SelectedPreset = Presets.First(x => x.Preset == Preset.Custom);
        _bulk = false;
        Recount();
    }

    public void SelectAll(bool value)
    {
        _bulk = true;
        foreach (var i in _all.Where(x => Matches(x))) i.IsSelected = value && i.Actionable;
        SelectedPreset = Presets.First(x => x.Preset == Preset.Custom);
        _bulk = false;
        Recount();
    }

    private void Recount() => SelectedCount = _all.Count(i => i.IsSelected && i.Actionable);

    private bool Matches(TweakItemViewModel i) =>
        string.IsNullOrWhiteSpace(Search)
        || i.Title.Contains(Search, StringComparison.CurrentCultureIgnoreCase)
        || i.Description.Contains(Search, StringComparison.CurrentCultureIgnoreCase)
        || i.Tweak.Id.Contains(Search, StringComparison.OrdinalIgnoreCase);

    private void BuildGroups()
    {
        Groups.Clear();
        var shown = _all.Where(Matches).ToList();
        VisibleCount = shown.Count;
        foreach (var g in shown.GroupBy(i => i.Tweak.Category).OrderBy(g => g.Key))
            Groups.Add(new CategoryGroupViewModel(g.Key, g));
    }
}
