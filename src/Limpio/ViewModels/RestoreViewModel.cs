using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Limpio.Core.Backup;
using Limpio.Core.Engine;
using Limpio.Services;

namespace Limpio.ViewModels;

public sealed class BackupItemViewModel
{
    public BackupFile File { get; }
    public string Title { get; }
    public string Subtitle { get; }

    public BackupItemViewModel(BackupFile f)
    {
        File = f;
        Title = f.CreatedUtc.ToLocalTime().ToString("f");
        var reg = f.Entries.Count(e => e.ActionKind == "registry");
        var svc = f.Entries.Count(e => e.ActionKind == "service");
        var tsk = f.Entries.Count(e => e.ActionKind == "scheduledTask");
        var apps = f.Entries.Sum(e => e.RemovedPackages.Count);
        Subtitle = Loc.F("restore.summary", reg, svc, tsk, apps);
    }
}

public sealed record RemovedAppItem(string Name, string StoreLink);

public partial class RestoreViewModel : ObservableObject
{
    private readonly AppState _app;

    public ObservableCollection<BackupItemViewModel> Backups { get; } = new();
    public ObservableCollection<RemovedAppItem> RemovedApps { get; } = new();
    public ObservableCollection<string> Log { get; } = new();
    public AppState App => _app;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelection))] [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    private BackupItemViewModel? _selected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "";

    public bool HasSelection => Selected is not null;
    public bool HasBackups => Backups.Count > 0;
    public string RevertText => _app.IsElevated ? Loc.T("restore.revert") : Loc.T("admin.restart");

    public RestoreViewModel(AppState app) => _app = app;

    partial void OnSelectedChanged(BackupItemViewModel? value)
    {
        RemovedApps.Clear();
        if (value is null) return;
        foreach (var p in value.File.Entries.SelectMany(e => e.RemovedPackages))
            RemovedApps.Add(new RemovedAppItem(p.Name, p.StoreLink));
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var list = await _app.Backups.ListAsync();
        Backups.Clear();
        foreach (var b in list) Backups.Add(new BackupItemViewModel(b));
        OnPropertyChanged(nameof(HasBackups));
        Selected = Backups.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RevertAsync()
    {
        if (!_app.IsElevated) { _app.RestartAsAdministrator("restore"); return; }
        if (Selected is null) return;
        IsBusy = true;
        Log.Clear();
        try
        {
            var report = await Task.Run(() => new RevertEngine(_app.Context)
                .RevertAsync(Selected.File, new Progress<ApplyEvent>(e =>
                    Log.Add($"{e.Target} - {Loc.T("result." + e.Result.ToString().ToLowerInvariant())}"
                            + (e.Result == StepResult.Failed ? ": " + e.Message : "")))));
            Status = Loc.F("restore.done", report.Applied, report.Failed);
            await _app.ScanAsync(measure: false);
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        await _app.Backups.DeleteAsync(Selected.File);
        await RefreshAsync();
    }

    [RelayCommand]
    private static void OpenStore(string link) =>
        Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
}
