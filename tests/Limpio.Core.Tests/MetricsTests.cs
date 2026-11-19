using Limpio.Core.Actions;
using Limpio.Core.Metrics;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Tests;

public class MetricsTests
{
    private static TweakState S(Category c, ActionState s, Risk risk = Risk.Safe) =>
        new(new Tweak { Id = Guid.NewGuid().ToString(), Category = c, Risk = risk }, s, new[] { s });

    [Fact]
    public void Privacy_score_is_zero_when_nothing_applied_and_100_when_all_applied()
    {
        var none = new[] { S(Category.Telemetry, ActionState.NotApplied), S(Category.Ads, ActionState.NotApplied) };
        var all = new[] { S(Category.Telemetry, ActionState.Applied), S(Category.Ads, ActionState.Applied) };
        Assert.Equal(0, PrivacyScore.Compute(none));
        Assert.Equal(100, PrivacyScore.Compute(all));
    }

    [Fact]
    public void Privacy_score_is_weighted_by_category()
    {
        // telemetry weight 4 applied, apps weight 1 not applied -> 4/5
        var s = new[] { S(Category.Telemetry, ActionState.Applied), S(Category.Apps, ActionState.NotApplied) };
        Assert.Equal(80, PrivacyScore.Compute(s));
    }

    [Fact]
    public void Privacy_score_counts_not_present_as_satisfied_and_ignores_advanced()
    {
        var s = new[]
        {
            S(Category.Apps, ActionState.NotPresent),
            S(Category.Privacy, ActionState.NotApplied, Risk.Advanced),
        };
        Assert.Equal(100, PrivacyScore.Compute(s));
        Assert.Equal(100, PrivacyScore.Compute(Array.Empty<TweakState>()));
    }

    [Fact]
    public void Diff_math_and_direction_of_improvement()
    {
        var before = new SystemSnapshot { RamUsedMb = 5000, ProcessCount = 200, PrivacyScore = 30, ActiveTelemetryServices = 2 };
        var after = new SystemSnapshot { RamUsedMb = 4200, ProcessCount = 180, PrivacyScore = 90, ActiveTelemetryServices = 0 };
        var d = SnapshotDiff.Compare(before, after).ToDictionary(x => x.Key);

        Assert.Equal(-800, d["ram"].Delta);
        Assert.True(d["ram"].Improved);
        Assert.Equal(-20, d["processes"].Delta);
        Assert.True(d["privacy"].Improved);
        Assert.Equal(60, d["privacy"].Delta);
        Assert.True(d["telemetry"].Improved);
        Assert.False(d["apps"].Improved || d["apps"].Worsened);
    }

    [Fact]
    public void Worse_values_are_flagged()
    {
        var d = SnapshotDiff.Compare(new SystemSnapshot { RamUsedMb = 100, PrivacyScore = 50 },
            new SystemSnapshot { RamUsedMb = 200, PrivacyScore = 40 }).ToDictionary(x => x.Key);
        Assert.True(d["ram"].Worsened);
        Assert.True(d["privacy"].Worsened);
    }

    [Fact]
    public async Task Snapshot_service_collects_metrics_from_fakes()
    {
        var w = new World();
        w.Appx.Add("Microsoft.BingNews").Add("Microsoft.WindowsStore").Add("Microsoft.VCLibs.140", framework: true);
        w.Services.Items["DiagTrack"] = new ServiceInfo("DiagTrack", StartType.Automatic, true);
        w.Services.Items["Spooler"] = new ServiceInfo("Spooler", StartType.Automatic, true);
        w.Services.Items["Idle"] = new ServiceInfo("Idle", StartType.Manual, false);
        w.Tasks.Items["\\A"] = true; w.Tasks.Items["\\B"] = false;

        var tweaks = new[] { new Tweak { Id = "n", Actions = { new AppxAction { PackageName = "Microsoft.BingNews" } } } };
        var svc = new SnapshotService(w.Context, new FakeProc { Processes = 150, Ram = 6000 }, new FakeStartup { Count = 7 },
            _ => 3 * 1024 * 1024, tweaks);

        var snap = await svc.CaptureAsync(Array.Empty<TweakState>());

        Assert.Equal(150, snap.ProcessCount);
        Assert.Equal(6000, snap.RamUsedMb);
        Assert.Equal(2, snap.RunningServices);
        Assert.Equal(2, snap.AppxCount);
        Assert.Equal(1, snap.EnabledTasks);
        Assert.Equal(7, snap.StartupEntries);
        Assert.Equal(1, snap.ActiveTelemetryServices);
        Assert.Equal(100, snap.PrivacyScore);
    }

    [Fact]
    public async Task Snapshot_store_round_trips_baseline()
    {
        var dir = Path.Combine(Path.GetTempPath(), "limpio-snap-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SnapshotStore(dir);
            Assert.Null(await store.LoadBaselineAsync());
            await store.SaveBaselineAsync(new SystemSnapshot { RamUsedMb = 1234, PrivacyScore = 42 });
            var b = await store.LoadBaselineAsync();
            Assert.Equal(1234, b!.RamUsedMb);
            Assert.Equal(42, b.PrivacyScore);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Markdown_export_contains_every_metric_row()
    {
        var md = ComparisonExporter.ToMarkdown(new SystemSnapshot { RamUsedMb = 100 }, new SystemSnapshot { RamUsedMb = 90 }, k => k);
        Assert.Contains("| ram | 100 | 90 | -10 |", md);
        Assert.Equal(9, md.Split('\n').Count(l => l.StartsWith("| ") && !l.StartsWith("| metric")));
    }
}
