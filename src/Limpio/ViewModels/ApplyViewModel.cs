using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Limpio.Core.Engine;
using Limpio.Core.Models;
using Limpio.Core.Platform;
using Limpio.Services;

namespace Limpio.ViewModels;

public sealed record LogLine(string Text, Brush Brush, string Glyph);

public partial class ApplyViewModel : ObservableObject
{
    private readonly AppState _app;
    private readonly TweaksViewModel _tweaks;

    public ObservableCollection<TweakItemViewModel> Summary { get; } = new();
    public ObservableCollection<LogLine> Log { get; } = new();
    public AppState App => _app;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private string? _restoreWarning;

    public bool HasSummary => Summary.Count > 0;
    public bool IsIdle => !IsRunning;
    public string ConfirmText => Loc.F("apply.confirm", Summary.Count);
    public string ButtonText => !_app.IsElevated ? Loc.T("admin.restart")
        : Loc.F("apply.button", Summary.Count);
    public bool CanStart => IsIdle && (Summary.Count > 0 || !_app.IsElevated);

    public ApplyViewModel(AppState app, TweaksViewModel tweaks)
    {
        _app = app;
        _tweaks = tweaks;
        tweaks.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TweaksViewModel.SelectedCount)) Refresh(); };
        Refresh();
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
    }

    public void Refresh()
    {
        Summary.Clear();
        foreach (var i in _tweaks.AllItems.Where(i => i.IsSelected && i.Actionable))
            Summary.Add(i);
        OnPropertyChanged(nameof(HasSummary));
        OnPropertyChanged(nameof(ConfirmText));
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(CanStart));
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!_app.IsElevated)
        {
            _app.RestartAsAdministrator("apply", _tweaks.SelectedTweaks.Select(t => t.Id));
            return;
        }
        var tweaks = _tweaks.SelectedTweaks.ToList();
        if (tweaks.Count == 0) return;

        Log.Clear();
        IsDone = false; Progress = 0; ResultText = ""; RestoreWarning = null;
        IsRunning = true;
        try
        {
            var engine = new ApplyEngine(_app.Context, _app.Backups, new WindowsRestorePointService());
            var progress = new Progress<ApplyEvent>(OnEvent);
            Log.Add(new LogLine(Loc.T("apply.log.restore"), Brush("#8A8A8A"), "Info"));
            var report = await Task.Run(() => engine.ApplyAsync(tweaks, progress));
            RestoreWarning = report.RestorePointWarning;
            ResultText = Loc.F("apply.result", report.Applied, report.Failed)
                         + (report.BackupPath is null ? "" : "\n" + Loc.F("apply.backup", report.BackupPath));
            IsDone = true;
            Progress = 100;
            await _app.ScanAsync(measure: false);
            Refresh();
        }
        catch (Exception ex)
        {
            ResultText = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void OnEvent(ApplyEvent e)
    {
        Progress = e.Total == 0 ? 0 : e.Done * 100.0 / e.Total;
        var (brush, glyph) = e.Result switch
        {
            StepResult.Applied => (Brush("#2E9E5B"), "CheckmarkCircle24"),
            StepResult.Failed => (Brush("#D13438"), "DismissCircle24"),
            StepResult.Protected => (Brush("#D98E04"), "ShieldError24"),
            _ => (Brush("#8A8A8A"), "SubtractCircle24"),
        };
        var title = _tweaks.AllItems.FirstOrDefault(i => i.Tweak.Id == e.TweakId)?.Title ?? e.TweakId;
        Log.Add(new LogLine($"{title}  -  {e.Target}  -  {Loc.T("result." + e.Result.ToString().ToLowerInvariant())}"
                            + (e.Result == StepResult.Failed ? $": {e.Message}" : ""), brush, glyph));
    }

    private static Brush Brush(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }
}
