using System.Text.Json;
using Limpio.Core.Actions;
using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Metrics;

public sealed class SystemSnapshot
{
    public DateTime TakenUtc { get; set; } = DateTime.UtcNow;
    public string Machine { get; set; } = Environment.MachineName;
    public int ProcessCount { get; set; }
    public long RamUsedMb { get; set; }
    public int RunningServices { get; set; }
    public int AppxCount { get; set; }
    public int EnabledTasks { get; set; }
    public int StartupEntries { get; set; }
    public long RemovablePackagesMb { get; set; }
    public int ActiveTelemetryServices { get; set; }
    public int PrivacyScore { get; set; }
}

public interface ISystemSnapshot
{
    Task<SystemSnapshot> CaptureAsync(IReadOnlyList<TweakState> states, CancellationToken ct = default);
}

/// <summary>One metric of a comparison: values before and after and whether lower is better.</summary>
public sealed record MetricDelta(string Key, double Before, double After, bool LowerIsBetter)
{
    public double Delta => After - Before;
    public bool Improved => LowerIsBetter ? After < Before : After > Before;
    public bool Worsened => LowerIsBetter ? After > Before : After < Before;
}

public static class SnapshotDiff
{
    public static IReadOnlyList<MetricDelta> Compare(SystemSnapshot before, SystemSnapshot after) => new[]
    {
        new MetricDelta("ram", before.RamUsedMb, after.RamUsedMb, true),
        new MetricDelta("processes", before.ProcessCount, after.ProcessCount, true),
        new MetricDelta("services", before.RunningServices, after.RunningServices, true),
        new MetricDelta("apps", before.AppxCount, after.AppxCount, true),
        new MetricDelta("tasks", before.EnabledTasks, after.EnabledTasks, true),
        new MetricDelta("startup", before.StartupEntries, after.StartupEntries, true),
        new MetricDelta("disk", before.RemovablePackagesMb, after.RemovablePackagesMb, true),
        new MetricDelta("telemetry", before.ActiveTelemetryServices, after.ActiveTelemetryServices, true),
        new MetricDelta("privacy", before.PrivacyScore, after.PrivacyScore, false),
    };
}

public static class PrivacyScore
{
    public static readonly IReadOnlyDictionary<Category, int> Weights = new Dictionary<Category, int>
    {
        [Category.Telemetry] = 4,
        [Category.Privacy] = 4,
        [Category.Ads] = 3,
        [Category.AI] = 3,
        [Category.ScheduledTasks] = 2,
        [Category.Services] = 2,
        [Category.Apps] = 1,
    };

    /// <summary>
    /// Weighted share of tweaks already satisfied (applied or not present), 0 to 100.
    /// Advanced tweaks are left out so the score reflects the safe baseline.
    /// </summary>
    public static int Compute(IEnumerable<TweakState> states)
    {
        double total = 0, good = 0;
        foreach (var s in states.Where(s => s.Tweak.Risk != Risk.Advanced))
        {
            var w = Weights.TryGetValue(s.Tweak.Category, out var x) ? x : 1;
            total += w;
            if (s.State != ActionState.NotApplied) good += w;
        }
        return total == 0 ? 100 : (int)Math.Round(good / total * 100);
    }
}

public sealed class SnapshotService : ISystemSnapshot
{
    private static readonly string[] TelemetryServices = { "DiagTrack", "dmwappushservice" };

    private readonly SystemContext _ctx;
    private readonly IProcessInfo _proc;
    private readonly IStartupAccess _startup;
    private readonly Func<string, long> _folderSizeBytes;
    private readonly IEnumerable<string> _removablePatterns;

    public SnapshotService(SystemContext ctx, IProcessInfo proc, IStartupAccess startup,
        Func<string, long> folderSizeBytes, IEnumerable<Tweak> catalogTweaks)
    {
        _ctx = ctx; _proc = proc; _startup = startup; _folderSizeBytes = folderSizeBytes;
        _removablePatterns = catalogTweaks.SelectMany(t => t.Actions).OfType<AppxAction>()
            .Where(a => !a.IsProtected).Select(a => a.PackageName).ToList();
    }

    public async Task<SystemSnapshot> CaptureAsync(IReadOnlyList<TweakState> states, CancellationToken ct = default)
    {
        var apps = (await _ctx.Appx.ListAsync(refresh: true)).Where(a => !a.IsFramework).ToList();
        var removable = apps.Where(a => !ProtectedList.IsProtectedPackage(a.Name)
                                        && _removablePatterns.Any(p => ProtectedList.WildcardMatch(p, a.Name)));
        long bytes = 0;
        foreach (var a in removable)
            if (!string.IsNullOrEmpty(a.InstallLocation)) bytes += _folderSizeBytes(a.InstallLocation);

        var services = _ctx.Services.ListAll();
        return new SystemSnapshot
        {
            ProcessCount = _proc.ProcessCount(),
            RamUsedMb = _proc.RamUsedMb(),
            RunningServices = services.Count(s => s.Running),
            AppxCount = apps.Count,
            EnabledTasks = await _ctx.Tasks.CountEnabledAsync(),
            StartupEntries = _startup.CountStartupEntries(),
            RemovablePackagesMb = bytes / (1024 * 1024),
            ActiveTelemetryServices = TelemetryServices.Count(n => _ctx.Services.Get(n)?.Running == true),
            PrivacyScore = PrivacyScore.Compute(states),
        };
    }
}

public sealed class SnapshotStore
{
    private readonly string _folder;

    public SnapshotStore(string? folder = null) => _folder = folder ?? AppPaths.Snapshots;

    private string BaselinePath => Path.Combine(_folder, "baseline.json");
    private string LatestPath => Path.Combine(_folder, "latest.json");

    public Task SaveBaselineAsync(SystemSnapshot s) => Write(BaselinePath, s);
    public Task SaveLatestAsync(SystemSnapshot s) => Write(LatestPath, s);
    public Task<SystemSnapshot?> LoadBaselineAsync() => Read(BaselinePath);
    public Task<SystemSnapshot?> LoadLatestAsync() => Read(LatestPath);

    private async Task Write(string path, SystemSnapshot s)
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(s, Json.Options));
        // keep a timestamped history as well
        var hist = Path.Combine(_folder, s.TakenUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss") + ".json");
        await File.WriteAllTextAsync(hist, JsonSerializer.Serialize(s, Json.Options));
    }

    private static async Task<SystemSnapshot?> Read(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<SystemSnapshot>(await File.ReadAllTextAsync(path), Json.Options); }
        catch (JsonException) { return null; }
    }
}

public static class ComparisonExporter
{
    public static string ToMarkdown(SystemSnapshot before, SystemSnapshot after, Func<string, string> label)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("| " + label("metric") + " | " + label("before") + " | " + label("after") + " | Delta |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var d in SnapshotDiff.Compare(before, after))
        {
            var sign = d.Delta > 0 ? "+" : "";
            sb.AppendLine($"| {label(d.Key)} | {d.Before:0} | {d.After:0} | {sign}{d.Delta:0} |");
        }
        sb.AppendLine();
        sb.AppendLine($"{label("measuredWith")} Limpio - {before.TakenUtc.ToLocalTime():yyyy-MM-dd HH:mm} / {after.TakenUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        return sb.ToString();
    }
}
