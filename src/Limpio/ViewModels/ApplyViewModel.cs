using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
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
    [ObservableProperty] private bool _protectionOff;
    [ObservableProperty] private bool _enableProtection;
    [ObservableProperty] private string? _logPath;

    public bool HasLog => LogPath is not null;
    public string ProtectionWarning => Loc.F("apply.protection.off", Drive);
    public string ProtectionCheckText => Loc.F("apply.protection.enable", Drive);
    private static string Drive => WindowsRestorePointService.SystemDrive.TrimEnd('\\');

    partial void OnLogPathChanged(string? value) => OnPropertyChanged(nameof(HasLog));

    public bool HasSummary => Summary.Count > 0;
    public bool IsIdle => !IsRunning;
    public string ConfirmText => Loc.P("apply.confirm", Summary.Count);
    public string ButtonText => !_app.IsElevated ? Loc.T("admin.restart")
        : Loc.P("apply.button", Summary.Count);
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

    /// <summary>Checks whether System Protection is off on the system drive (only readable when elevated).</summary>
    public async Task ProbeProtectionAsync()
    {
        if (!_app.IsElevated) return;
        var enabled = await new WindowsRestorePointService(Loc.Instance.Language).IsProtectionEnabledAsync();
        ProtectionOff = enabled == false;
        if (!ProtectionOff) EnableProtection = false;
    }

    [RelayCommand]
    private void OpenLog()
    {
        if (LogPath is null || !File.Exists(LogPath)) return;
        Process.Start(new ProcessStartInfo(LogPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private static void CopyLine(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Clipboard.SetText(text); } catch { /* clipboard busy */ }
    }

    private RunLogWriter? _runLog;

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
        IsDone = false; Progress = 0; ResultText = ""; RestoreWarning = null; LogPath = null;
        _runLog = TryCreateLog();
        IsRunning = true;
        try
        {
            var engine = new ApplyEngine(_app.Context, _app.Backups, new WindowsRestorePointService(Loc.Instance.Language));
            var progress = new Progress<ApplyEvent>(OnEvent);
            Log.Add(new LogLine(Loc.T("apply.log.restore"), Brush("#8A8A8A"), "Info"));
            var options = new ApplyOptions { EnableSystemProtection = EnableProtection };
            var report = await Task.Run(() => engine.ApplyAsync(tweaks, progress, default, options));
            RestoreWarning = string.IsNullOrWhiteSpace(report.RestorePointWarning)
                ? null : Loc.F("apply.restoreFail", report.RestorePointWarning);
            var parts = new List<string> { Loc.P("apply.result.applied", report.Applied) };
            if (report.Blocked > 0) parts.Add(Loc.P("apply.result.blocked", report.Blocked));
            if (report.Failed > 0) parts.Add(Loc.P("apply.result.failed", report.Failed));
            ResultText = Loc.F("apply.result", string.Join(", ", parts))
                         + (report.BackupPath is null ? "" : "\n" + Loc.F("apply.backup", report.BackupPath));
            foreach (var (id, outcome) in report.Outcomes().Where(o => o.Value == TweakOutcome.PartiallyApplied))
                Log.Add(new LogLine($"{TitleOf(id)}  -  {Loc.T("result.partial")}: {Loc.T("apply.partial")}",
                    Brush("#D98E04"), "Warning24"));
            if (_runLog is not null)
            {
                SafeLog(() => _runLog.Summary(report));
                LogPath = _runLog.Path;
            }
            await ProbeProtectionAsync();
            IsDone = true;
            Progress = 100;
            await _app.ScanAsync(measure: false);
            Refresh();
        }
        catch (Exception ex)
        {
            ResultText = ex.Message;
            if (_runLog is not null) { SafeLog(() => _runLog.Line("ERROR " + ex)); LogPath = _runLog.Path; }
        }
        finally
        {
            IsRunning = false;
        }
    }

    private RunLogWriter? TryCreateLog()
    {
        try
        {
            var w = new RunLogWriter();
            w.Line($"Limpio run {DateTime.Now:yyyy-MM-dd HH:mm:ss}  machine={Environment.MachineName}  elevated={_app.IsElevated}");
            return w;
        }
        catch { return null; }
    }

    private static void SafeLog(Action a)
    {
        try { a(); } catch { /* logging must never break a run */ }
    }

    private string TitleOf(string id) => _tweaks.AllItems.FirstOrDefault(i => i.Tweak.Id == id)?.Title ?? id;

    private void OnEvent(ApplyEvent e)
    {
        if (_runLog is not null) SafeLog(() => _runLog.Event(e));
        Progress = e.Total == 0 ? 0 : e.Done * 100.0 / e.Total;
        var (brush, glyph) = e.Result switch
        {
            StepResult.Applied => (Brush("#2E9E5B"), "CheckmarkCircle24"),
            StepResult.Failed => (Brush("#D13438"), "DismissCircle24"),
            StepResult.Protected => (Brush("#D98E04"), "ShieldError24"),
            StepResult.Blocked => (Brush("#D98E04"), "ShieldError24"),
            _ => (Brush("#8A8A8A"), "SubtractCircle24"),
        };
        var title = TitleOf(e.TweakId);
        var text = $"{title}  -  {e.Target}  -  {Loc.T("result." + e.Result.ToString().ToLowerInvariant())}";
        if (e.Result == StepResult.Failed) text += $": {e.Message}";
        else if (e.Result == StepResult.Blocked)
            text += $": {(e.UcpdActive ? Loc.T("apply.ucpd") : Loc.T("apply.blockedNoUcpd"))}";
        Log.Add(new LogLine(text, brush, glyph));
    }

    private static Brush Brush(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }
}
