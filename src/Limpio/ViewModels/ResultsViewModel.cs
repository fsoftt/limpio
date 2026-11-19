using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Limpio.Core.Metrics;
using Limpio.Services;

namespace Limpio.ViewModels;

public sealed class MetricCardViewModel
{
    public string Key { get; }
    public string Label { get; }
    public string BeforeText { get; }
    public string AfterText { get; }
    public string DeltaText { get; }
    public string ArrowGlyph { get; }
    public Brush DeltaBrush { get; }
    public double BeforeRatio { get; }
    public double AfterRatio { get; }
    public Brush AfterBarBrush => DeltaBrush;

    public MetricCardViewModel(MetricDelta d)
    {
        Key = d.Key;
        Label = Loc.T("m." + d.Key);
        BeforeText = Format(d.Key, d.Before);
        AfterText = Format(d.Key, d.After);
        var sign = d.Delta > 0 ? "+" : "";
        DeltaText = d.Delta == 0 ? "=" : sign + Format(d.Key, d.Delta);
        ArrowGlyph = d.Delta == 0 ? "Subtract24" : d.Delta < 0 ? "ArrowDown24" : "ArrowUp24";
        DeltaBrush = d.Improved ? Solid(0x2E, 0x9E, 0x5B) : d.Worsened ? Solid(0xD1, 0x34, 0x38) : Solid(0x8A, 0x8A, 0x8A);
        var max = Math.Max(Math.Max(d.Before, d.After), 1);
        BeforeRatio = d.Key == "privacy" ? d.Before / 100 : d.Before / max;
        AfterRatio = d.Key == "privacy" ? d.After / 100 : d.After / max;
    }

    private static string Format(string key, double v) => key switch
    {
        "ram" or "disk" => $"{v:0} MB",
        "privacy" => $"{v:0}/100",
        _ => $"{v:0}",
    };

    private static SolidColorBrush Solid(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}

public partial class ResultsViewModel : ObservableObject
{
    private readonly AppState _app;

    public ObservableCollection<MetricCardViewModel> Cards { get; } = new();

    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private bool _isMeasuring;
    [ObservableProperty] private string _beforeDate = "";
    [ObservableProperty] private string _afterDate = "";
    [ObservableProperty] private int _beforeScore;
    [ObservableProperty] private int _afterScore;
    [ObservableProperty] private string _status = "";
    public bool IsDemo => _app.Options.Demo;

    public ResultsViewModel(AppState app)
    {
        _app = app;
        app.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.Baseline) or nameof(AppState.Latest)) Rebuild();
        };
        Rebuild();
    }

    private void Rebuild()
    {
        Cards.Clear();
        var b = _app.Baseline;
        var a = _app.Latest;
        HasData = b is not null && a is not null;
        if (!HasData) return;
        BeforeDate = b!.TakenUtc.ToLocalTime().ToString("g");
        AfterDate = a!.TakenUtc.ToLocalTime().ToString("g");
        BeforeScore = b.PrivacyScore;
        AfterScore = a.PrivacyScore;
        foreach (var d in SnapshotDiff.Compare(b, a)) Cards.Add(new MetricCardViewModel(d));
    }

    [RelayCommand]
    private async Task MeasureAsync()
    {
        IsMeasuring = true;
        try { await _app.ScanAsync(measure: true); }
        finally { IsMeasuring = false; }
    }

    [RelayCommand]
    private async Task ResetBaselineAsync()
    {
        await _app.UseLatestAsBaselineAsync();
        Status = Loc.T("results.baselineReset");
    }

    public string BuildMarkdown() =>
        ComparisonExporter.ToMarkdown(_app.Baseline!, _app.Latest!, k => Loc.T(k is "metric" or "before" or "after" or "measuredWith" ? "md." + k : "m." + k));

    public void SaveMarkdown(string path)
    {
        File.WriteAllText(path, "# Limpio\n\n" + BuildMarkdown());
        Status = Loc.F("results.saved", path);
    }
}
