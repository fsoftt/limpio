using System.Management;
using System.Text.Json;
using Limpio.Core;
using Limpio.Core.Actions;
using Limpio.Core.Catalog;
using Limpio.Core.Engine;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Tests;

public class BlockedByWindowsTests
{
    private static RegistryAction Reg(string name) => new()
    {
        Hive = "HKLM", Path = @"SOFTWARE\Policies\Microsoft\Dsh", Name = name, ValueType = "dword",
        Value = JsonSerializer.SerializeToElement(0),
    };

    private static Tweak Widgets(World w)
    {
        w.Appx.Add("MicrosoftWindows.Client.WebExperience");
        var t = new Tweak { Id = "apps.widgets", Category = Category.Apps };
        t.Actions.Add(new AppxAction { PackageName = "MicrosoftWindows.Client.WebExperience" });
        t.Actions.Add(Reg("AllowNewsAndInterests"));
        return t;
    }

    [Fact]
    public async Task Unauthorized_registry_write_is_blocked_not_failed()
    {
        var w = new World();
        w.Registry.DenyWrite.Add("AllowNewsAndInterests");
        var store = new MemoryBackupStore();
        var report = await new ApplyEngine(w.Context, store, new FakeRestore()).ApplyAsync(new[] { Widgets(w) });

        Assert.Equal(1, report.Blocked);
        Assert.Equal(0, report.Failed);
        Assert.Equal(1, report.Applied);
        Assert.Contains(report.Events, e => e.Result == StepResult.Blocked && e.Target.EndsWith("AllowNewsAndInterests"));
        // the package removal still counts and is in the backup
        Assert.Single(Assert.Single(store.Saved).Entries);
    }

    [Fact]
    public async Task Security_exception_is_also_blocked()
    {
        var w = new World();
        w.Registry.DenyWrite.Add("AllowNewsAndInterests");
        w.Registry.DenyWith = new System.Security.SecurityException("denied");
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new FakeRestore()).ApplyAsync(new[] { Widgets(w) });
        Assert.Equal(1, report.Blocked);
    }

    [Fact]
    public async Task Hint_flag_follows_the_ucpd_service_state()
    {
        foreach (var running in new[] { true, false })
        {
            var w = new World();
            w.Registry.DenyWrite.Add("AllowNewsAndInterests");
            w.Services.Items["UCPD"] = new ServiceInfo("UCPD", StartType.Automatic, running);
            var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new FakeRestore()).ApplyAsync(new[] { Widgets(w) });
            Assert.Equal(running, report.Events.Single(e => e.Result == StepResult.Blocked).UcpdActive);
        }
    }

    [Fact]
    public async Task Some_actions_ok_and_some_blocked_is_partially_applied()
    {
        var w = new World();
        w.Registry.DenyWrite.Add("AllowNewsAndInterests");
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new FakeRestore()).ApplyAsync(new[] { Widgets(w) });
        Assert.Equal(TweakOutcome.PartiallyApplied, report.Outcomes()["apps.widgets"]);
    }

    [Fact]
    public async Task Package_already_gone_plus_blocked_value_is_still_partial()
    {
        // second run of the same tweak: appx is not present anymore
        var w = new World();
        w.Registry.DenyWrite.Add("AllowNewsAndInterests");
        var t = new Tweak { Id = "apps.widgets", Category = Category.Apps };
        t.Actions.Add(new AppxAction { PackageName = "MicrosoftWindows.Client.WebExperience" });
        t.Actions.Add(Reg("AllowNewsAndInterests"));
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new FakeRestore()).ApplyAsync(new[] { t });
        Assert.Equal(TweakOutcome.PartiallyApplied, report.Outcomes()["apps.widgets"]);
    }

    [Fact]
    public async Task Only_blocked_actions_is_blocked_outcome()
    {
        var w = new World();
        w.Registry.DenyWrite.Add("AllowNewsAndInterests");
        var t = new Tweak { Id = "x", Category = Category.Apps };
        t.Actions.Add(Reg("AllowNewsAndInterests"));
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new FakeRestore()).ApplyAsync(new[] { t });
        Assert.Equal(TweakOutcome.Blocked, report.Outcomes()["x"]);
    }

    [Fact]
    public async Task Other_exceptions_stay_failures()
    {
        var w = new World();
        w.Registry.DenyWrite.Add("AllowNewsAndInterests");
        w.Registry.DenyWith = new InvalidOperationException("boom");
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new FakeRestore()).ApplyAsync(new[] { Widgets(w) });
        Assert.Equal(1, report.Failed);
        Assert.Equal(0, report.Blocked);
        Assert.Equal(TweakOutcome.Failed, report.Outcomes()["apps.widgets"]);
    }

    [Fact]
    public void Catalog_has_no_taskbar_value_that_ucpd_blocks()
    {
        var cat = TweakCatalog.LoadEmbedded();
        var names = cat.Tweaks.SelectMany(t => t.Actions).OfType<RegistryAction>().Select(a => a.Name).ToList();
        Assert.DoesNotContain("TaskbarDa", names);
    }

    [Fact]
    public void Recall_tweak_sets_both_hklm_and_hkcu_policy()
    {
        var recall = TweakCatalog.LoadEmbedded().Tweaks.Single(t => t.Id == "ai.recall");
        var hives = recall.Actions.OfType<RegistryAction>().Where(a => a.Name == "DisableAIDataAnalysis").Select(a => a.Hive).ToList();
        Assert.Contains("HKLM", hives);
        Assert.Contains("HKCU", hives);
    }
}

public class RestorePointReasonTests
{
    [Theory]
    [InlineData(5u, "denegado", "denied")]
    [InlineData(1058u, "deshabilitado", "disabled")]
    [InlineData(112u, "espacio", "space")]
    [InlineData(183u, "en curso", "already running")]
    [InlineData(9999u, "9999", "9999")]
    public void Return_codes_map_to_text(uint code, string es, string en)
    {
        var t = RestorePointReasons.FromReturnCode(code);
        Assert.Contains(es, t.Es);
        Assert.Contains(en, t.En);
    }

    [Fact]
    public void Empty_exception_message_still_gives_a_reason()
    {
        var ex = new ManagementException("");
        var t = RestorePointReasons.FromException(ex);
        Assert.False(string.IsNullOrWhiteSpace(t.En));
        Assert.Contains("ManagementException", t.En);
        Assert.Contains("HRESULT 0x", t.En);
    }

    [Fact]
    public void Access_denied_gets_a_hint()
    {
        var t = RestorePointReasons.FromException(new UnauthorizedAccessException());
        Assert.Contains("administrator", t.En);
        Assert.Contains("administrador", t.Es);
    }

    [Fact]
    public void Disabled_protection_names_the_drive()
    {
        Assert.Contains("C:", RestorePointReasons.Disabled("C:").En);
        Assert.Contains("C:", RestorePointReasons.Disabled("C:").Es);
    }

    [Fact]
    public async Task Engine_never_reports_an_empty_reason_when_the_service_throws_silently()
    {
        var report = await new ApplyEngine(new World().Context, new MemoryBackupStore(), new SilentThrow())
            .ApplyAsync(Array.Empty<Tweak>());
        Assert.False(string.IsNullOrWhiteSpace(report.RestorePointWarning));
    }

    private sealed class SilentThrow : IRestorePointService
    {
        public Task<string?> CreateAsync(string description) => throw new InvalidOperationException("");
    }

    [Fact]
    public async Task Protection_is_enabled_only_when_opted_in_and_off()
    {
        var tweak = new Tweak { Id = "t", Category = Category.Apps };
        var off = new FakeRestore { ProtectionEnabled = false };
        await new ApplyEngine(new World().Context, new MemoryBackupStore(), off)
            .ApplyAsync(new[] { tweak }, options: new ApplyOptions { EnableSystemProtection = false });
        Assert.Equal(0, off.EnableCalls);

        var optIn = new FakeRestore { ProtectionEnabled = false };
        var report = await new ApplyEngine(new World().Context, new MemoryBackupStore(), optIn)
            .ApplyAsync(new[] { tweak }, options: new ApplyOptions { EnableSystemProtection = true });
        Assert.Equal(1, optIn.EnableCalls);
        Assert.Equal(1, optIn.Calls);
        Assert.Null(report.RestorePointWarning);

        var already = new FakeRestore { ProtectionEnabled = true };
        await new ApplyEngine(new World().Context, new MemoryBackupStore(), already)
            .ApplyAsync(new[] { tweak }, options: new ApplyOptions { EnableSystemProtection = true });
        Assert.Equal(0, already.EnableCalls);
    }

    [Fact]
    public async Task Failing_to_enable_protection_is_reported_and_run_continues()
    {
        var w = new World();
        var restore = new FakeRestore { ProtectionEnabled = false, EnableResult = "no way" };
        var t = new Tweak { Id = "t", Category = Category.Privacy };
        t.Actions.Add(new RegistryAction
        {
            Hive = "HKCU", Path = "Software\\T", Name = "X", ValueType = "dword", Value = JsonSerializer.SerializeToElement(0),
        });
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), restore)
            .ApplyAsync(new[] { t }, options: new ApplyOptions { EnableSystemProtection = true });
        Assert.Equal("no way", report.RestorePointWarning);
        Assert.Equal(0, restore.Calls);
        Assert.Equal(1, report.Applied);
    }
}

public sealed class RunLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "limpio-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Log_file_is_named_by_timestamp_and_created_in_the_folder()
    {
        var log = new RunLogWriter(new DateTime(2026, 11, 19, 10, 22, 29), _dir);
        Assert.Equal(Path.Combine(_dir, "20261119-102229.log"), log.Path);
        Assert.True(Directory.Exists(_dir));
    }

    [Fact]
    public void Same_second_gets_a_distinct_file()
    {
        var at = new DateTime(2026, 11, 19, 10, 22, 29);
        var a = new RunLogWriter(at, _dir);
        a.Line("first");
        var b = new RunLogWriter(at, _dir);
        Assert.NotEqual(a.Path, b.Path);
    }

    [Fact]
    public async Task Events_and_summary_are_written()
    {
        var w = new World();
        w.Registry.DenyWrite.Add("AllowNewsAndInterests");
        var t = new Tweak { Id = "apps.widgets", Category = Category.Apps };
        t.Actions.Add(new RegistryAction
        {
            Hive = "HKLM", Path = "P", Name = "AllowNewsAndInterests", ValueType = "dword", Value = JsonSerializer.SerializeToElement(0),
        });
        var log = new RunLogWriter(null, _dir);
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new FakeRestore())
            .ApplyAsync(new[] { t }, new SyncProgress(log));
        log.Summary(report);

        var text = await File.ReadAllTextAsync(log.Path);
        Assert.Contains("Blocked", text);
        Assert.Contains("apps.widgets", text);
        Assert.Contains("Blocked=1", text);
        Assert.Contains("tweak apps.widgets: Blocked", text);
    }

    [Fact]
    public void Multi_line_messages_stay_on_one_line()
    {
        var line = RunLogWriter.Format(new ApplyEvent("t", "x", StepResult.Failed, "a\r\nb", 1, 2));
        Assert.DoesNotContain("\n", line);
        Assert.Contains("a  b", line.Replace("a b", "a  b"));
    }

    private sealed class SyncProgress(RunLogWriter log) : IProgress<ApplyEvent>
    {
        public void Report(ApplyEvent value) => log.Event(value);
    }
}
