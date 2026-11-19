using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Limpio.Core;
using Limpio.Core.Backup;
using Limpio.Core.Catalog;
using Limpio.Core.Metrics;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Services;

public sealed class StartupOptions
{
    public string? Page { get; set; }
    public string? Preset { get; set; }
    public HashSet<string> Select { get; } = new();
    public bool Demo { get; set; }
    public string? Shot { get; set; }
    public string? Lang { get; set; }

    public static StartupOptions Parse(string[] args)
    {
        var o = new StartupOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--page" when i + 1 < args.Length: o.Page = args[++i]; break;
                case "--preset" when i + 1 < args.Length: o.Preset = args[++i]; break;
                case "--lang" when i + 1 < args.Length: o.Lang = args[++i]; break;
                case "--select" when i + 1 < args.Length:
                    foreach (var id in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)) o.Select.Add(id);
                    break;
                case "--demo": o.Demo = true; break;
                case "--shot" when i + 1 < args.Length: o.Shot = args[++i]; break;
            }
        }
        return o;
    }
}

/// <summary>Shared state: catalog, system adapters, last scan and snapshots.</summary>
public sealed partial class AppState : ObservableObject
{
    public TweakCatalog Catalog { get; }
    public SystemContext Context { get; }
    public bool IsElevated { get; }
    public IBackupStore Backups { get; } = new BackupStore();
    public SnapshotStore Snapshots { get; } = new SnapshotStore();
    public ISystemSnapshot SnapshotService { get; }
    public StartupOptions Options { get; }

    [ObservableProperty] private IReadOnlyList<TweakState> _states = Array.Empty<TweakState>();
    [ObservableProperty] private bool _hasScan;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private int _privacyScore;
    [ObservableProperty] private int _appliedCount;
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _notPresentCount;
    [ObservableProperty] private SystemSnapshot? _baseline;
    [ObservableProperty] private SystemSnapshot? _latest;

    public bool NotElevated => !IsElevated;
    public bool CanScan => !IsScanning;
    public string ElevationText => IsElevated ? Loc.T("home.elevated") : Loc.T("home.notElevated");

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(CanScan));

    public event Action? ScanCompleted;

    public AppState(StartupOptions options)
    {
        Options = options;
        Catalog = TweakCatalog.LoadEmbedded();
        IsElevated = new WindowsElevation().IsElevated;
        Context = WindowsSystem.CreateContext(IsElevated);
        SnapshotService = new SnapshotService(Context, new WindowsProcessInfo(), new WindowsStartupAccess(),
            WindowsSystem.FolderSizeBytes, Catalog.Tweaks);
    }

    public async Task ScanAsync(bool measure = true)
    {
        if (IsScanning) return;
        IsScanning = true;
        try
        {
            var states = await Task.Run(() => Scanner.ScanAsync(Catalog.Tweaks, Context));
            Update(states);
            if (measure) await MeasureAsync();
        }
        finally
        {
            IsScanning = false;
            ScanCompleted?.Invoke();
        }
    }

    private void Update(IReadOnlyList<TweakState> states)
    {
        States = states;
        HasScan = true;
        AppliedCount = states.Count(s => s.State == ActionState.Applied);
        PendingCount = states.Count(s => s.State == ActionState.NotApplied);
        NotPresentCount = states.Count(s => s.State == ActionState.NotPresent);
        PrivacyScore = Core.Metrics.PrivacyScore.Compute(states);
    }

    /// <summary>Takes a new snapshot ("after"). The first one ever becomes the baseline ("before").</summary>
    public async Task MeasureAsync()
    {
        if (Options.Demo) { LoadDemo(); return; }
        var snap = await Task.Run(() => SnapshotService.CaptureAsync(States));
        Baseline ??= await Snapshots.LoadBaselineAsync();
        if (Baseline is null)
        {
            await Snapshots.SaveBaselineAsync(snap);
            Baseline = snap;
        }
        await Snapshots.SaveLatestAsync(snap);
        Latest = snap;
    }

    public async Task UseLatestAsBaselineAsync()
    {
        if (Latest is null || Options.Demo) return;
        await Snapshots.SaveBaselineAsync(Latest);
        Baseline = Latest;
    }

    /// <summary>Synthetic numbers used only for screenshots (--demo). Nothing is saved to disk.</summary>
    private void LoadDemo()
    {
        Baseline = new SystemSnapshot
        {
            TakenUtc = DateTime.UtcNow.AddHours(-2), RamUsedMb = 6120, ProcessCount = 214, RunningServices = 142, AppxCount = 68,
            EnabledTasks = 231, StartupEntries = 14, RemovablePackagesMb = 1840, ActiveTelemetryServices = 2, PrivacyScore = 31,
        };
        Latest = new SystemSnapshot
        {
            TakenUtc = DateTime.UtcNow, RamUsedMb = 5240, ProcessCount = 187, RunningServices = 131, AppxCount = 49,
            EnabledTasks = 219, StartupEntries = 11, RemovablePackagesMb = 640, ActiveTelemetryServices = 0, PrivacyScore = 96,
        };
    }

    public async Task LoadSavedSnapshotsAsync()
    {
        if (Options.Demo) { LoadDemo(); return; }
        Baseline = await Snapshots.LoadBaselineAsync();
        Latest = await Snapshots.LoadLatestAsync();
    }

    public void RestartAsAdministrator(string? page = null, IEnumerable<string>? selected = null)
    {
        var args = new List<string>();
        if (page is not null) { args.Add("--page"); args.Add(page); }
        var sel = selected?.ToList();
        if (sel is { Count: > 0 }) { args.Add("--select"); args.Add(string.Join(',', sel)); }
        if (Options.Lang is not null) { args.Add("--lang"); args.Add(Options.Lang); }
        var psi = new ProcessStartInfo(Environment.ProcessPath ?? "Limpio.exe", string.Join(' ', args))
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };
        try
        {
            Process.Start(psi);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Win32Exception)
        {
            // user declined the UAC prompt
        }
    }
}
