using Limpio.Core;
using Limpio.Core.Actions;
using Limpio.Core.Backup;
using Limpio.Core.Engine;
using Limpio.Core.Models;
using Limpio.Core.Platform;
using System.Text.Json;

namespace Limpio.Core.Tests;

public class ProtectedListTests
{
    [Theory]
    [InlineData("Microsoft.WindowsStore")]
    [InlineData("Microsoft.StorePurchaseApp")]
    [InlineData("Microsoft.MicrosoftEdge.Stable")]
    [InlineData("Microsoft.DesktopAppInstaller")]
    [InlineData("Microsoft.HEVCVideoExtension")]
    [InlineData("Microsoft.WebpImageExtension")]
    [InlineData("Microsoft.WebMediaExtensions")]
    [InlineData("Microsoft.WindowsCalculator")]
    [InlineData("Microsoft.Windows.Photos")]
    [InlineData("Microsoft.Paint")]
    [InlineData("Microsoft.WindowsNotepad")]
    [InlineData("Microsoft.WindowsTerminal")]
    [InlineData("Microsoft.ScreenSketch")]
    [InlineData("Microsoft.SecHealthUI")]
    [InlineData("RealtekSemiconductorCorp.RealtekAudioControl")]
    [InlineData("DTSInc.DTSSoundUnbound")]
    [InlineData("AppUp.IntelGraphicsExperience")]
    public void Protected_packages_are_recognized(string name) =>
        Assert.True(ProtectedList.IsProtectedPackage(name));

    [Theory]
    [InlineData("Microsoft.BingNews")]
    [InlineData("Clipchamp.Clipchamp")]
    [InlineData("Microsoft.XboxIdentityProvider")]
    public void Normal_packages_are_not_protected(string name) =>
        Assert.False(ProtectedList.IsProtectedPackage(name));

    [Theory]
    [InlineData("wuauserv"), InlineData("UsoSvc"), InlineData("BITS"), InlineData("WinDefend"), InlineData("bits")]
    public void Protected_services(string name) => Assert.True(ProtectedList.IsProtectedService(name));

    [Fact]
    public async Task Engine_refuses_protected_actions_even_if_catalog_says_so()
    {
        var w = new World();
        w.Appx.Add("Microsoft.WindowsStore");
        w.Services.Items["wuauserv"] = new ServiceInfo("wuauserv", StartType.Manual, true);
        var tweak = new Tweak
        {
            Id = "evil", Category = Category.Apps,
            Actions =
            {
                new AppxAction { PackageName = "Microsoft.WindowsStore" },
                new ServiceAction { Name = "wuauserv", StartType = "Disabled" },
            },
        };
        var store = new MemoryBackupStore();
        var report = await new ApplyEngine(w.Context, store, new FakeRestore()).ApplyAsync(new[] { tweak });

        Assert.All(report.Events, e => Assert.Equal(StepResult.Protected, e.Result));
        Assert.Empty(w.Appx.Removed);
        Assert.Empty(w.Services.Log);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Wildcard_appx_never_removes_protected_matches()
    {
        var w = new World();
        w.Appx.Add("Vendor.IntelMcAfeeBundle").Add("Vendor.McAfeeSecurity");
        var action = new AppxAction { PackageName = "*McAfee*" };
        await action.ApplyAsync(w.Context);
        Assert.Equal(new[] { "Vendor.McAfeeSecurity" }, w.Appx.Removed);
    }
}

public class ActionTests
{
    [Fact]
    public async Task Registry_action_detect_apply_revert_when_value_existed()
    {
        var w = new World();
        w.Registry.SetValue("HKCU", "Software\\T", "V", "dword", 1);
        var a = new RegistryAction { Hive = "HKCU", Path = "Software\\T", Name = "V", ValueType = "dword", Value = JsonSerializer.SerializeToElement(0) };

        Assert.Equal(ActionState.NotApplied, await a.DetectAsync(w.Context));
        var entry = await a.ApplyAsync(w.Context);
        Assert.Equal(ActionState.Applied, await a.DetectAsync(w.Context));
        Assert.True(entry.Existed);
        Assert.Equal("1", entry.OldValue);

        await a.RevertAsync(w.Context, entry);
        Assert.Equal(1, w.Registry.GetValue("HKCU", "Software\\T", "V")!.Value);
    }

    [Fact]
    public async Task Registry_action_revert_deletes_value_that_did_not_exist()
    {
        var w = new World();
        var a = new RegistryAction { Hive = "HKLM", Path = "SOFTWARE\\P", Name = "N", ValueType = "dword", Value = JsonSerializer.SerializeToElement(1) };
        var entry = await a.ApplyAsync(w.Context);
        Assert.NotNull(w.Registry.GetValue("HKLM", "SOFTWARE\\P", "N"));
        await a.RevertAsync(w.Context, entry);
        Assert.Null(w.Registry.GetValue("HKLM", "SOFTWARE\\P", "N"));
    }

    [Fact]
    public async Task Service_action_disables_stops_and_reverts()
    {
        var w = new World();
        w.Services.Items["DiagTrack"] = new ServiceInfo("DiagTrack", StartType.Automatic, true);
        var a = new ServiceAction { Name = "DiagTrack", StartType = "Disabled" };

        Assert.Equal(ActionState.NotApplied, await a.DetectAsync(w.Context));
        var entry = await a.ApplyAsync(w.Context);
        Assert.Equal(ActionState.Applied, await a.DetectAsync(w.Context));
        Assert.Contains("stop DiagTrack", w.Services.Log);

        await a.RevertAsync(w.Context, entry);
        Assert.Equal(StartType.Automatic, w.Services.Get("DiagTrack")!.StartType);
    }

    [Fact]
    public async Task Missing_service_task_and_app_are_not_present()
    {
        var w = new World();
        Assert.Equal(ActionState.NotPresent, await new ServiceAction { Name = "Nope" }.DetectAsync(w.Context));
        Assert.Equal(ActionState.NotPresent, await new ScheduledTaskAction { Path = "\\Nope" }.DetectAsync(w.Context));
        Assert.Equal(ActionState.NotPresent, await new AppxAction { PackageName = "Nope.App" }.DetectAsync(w.Context));
    }

    [Fact]
    public async Task Task_action_disables_and_reverts()
    {
        var w = new World();
        w.Tasks.Items["\\A\\B"] = true;
        var a = new ScheduledTaskAction { Path = "\\A\\B" };
        var entry = await a.ApplyAsync(w.Context);
        Assert.Equal(ActionState.Applied, await a.DetectAsync(w.Context));
        await a.RevertAsync(w.Context, entry);
        Assert.True(w.Tasks.Items["\\A\\B"]);
    }

    [Fact]
    public async Task Appx_action_records_removed_package_with_store_link()
    {
        var w = new World();
        w.Appx.Add("Microsoft.BingNews");
        var entry = await new AppxAction { PackageName = "Microsoft.BingNews" }.ApplyAsync(w.Context);
        var pkg = Assert.Single(entry.RemovedPackages);
        Assert.Equal("ms-windows-store://pdp/?PFN=Microsoft.BingNews_abc", pkg.StoreLink);
    }
}

public class ApplyEngineTests
{
    private static Tweak T(string id, params TweakAction[] actions)
    {
        var t = new Tweak { Id = id, Category = Category.Privacy };
        t.Actions.AddRange(actions);
        return t;
    }

    private static RegistryAction Reg(string name, int value) => new()
    {
        Hive = "HKCU", Path = "Software\\T", Name = name, ValueType = "dword", Value = JsonSerializer.SerializeToElement(value),
    };

    [Fact]
    public async Task One_failure_does_not_stop_the_run_and_backup_is_saved()
    {
        var w = new World();
        w.Appx.Add("Bad.App").Add("Good.App");
        w.Appx.FailOn.Add("Bad.App");
        var store = new MemoryBackupStore();
        var restore = new FakeRestore { Result = "disabled" };
        var tweaks = new[]
        {
            T("a", new AppxAction { PackageName = "Bad.App" }),
            T("b", new AppxAction { PackageName = "Good.App" }, Reg("X", 0)),
        };
        var progress = new List<ApplyEvent>();
        var report = await new ApplyEngine(w.Context, store, restore).ApplyAsync(tweaks, new Progress<ApplyEvent>(progress.Add));

        Assert.Equal(1, report.Failed);
        Assert.Equal(2, report.Applied);
        Assert.Equal(1, restore.Calls);
        Assert.Equal("disabled", report.RestorePointWarning);
        var backup = Assert.Single(store.Saved);
        Assert.Equal(2, backup.Entries.Count);
        Assert.Equal("disabled", backup.RestorePointWarning);
    }

    [Fact]
    public async Task Already_applied_actions_are_skipped_and_not_backed_up()
    {
        var w = new World();
        w.Registry.SetValue("HKCU", "Software\\T", "X", "dword", 0);
        var store = new MemoryBackupStore();
        var report = await new ApplyEngine(w.Context, store, new FakeRestore()).ApplyAsync(new[] { T("a", Reg("X", 0)) });
        Assert.Equal(StepResult.Skipped, Assert.Single(report.Events).Result);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Restore_point_exception_becomes_a_warning()
    {
        var w = new World();
        var report = await new ApplyEngine(w.Context, new MemoryBackupStore(), new ThrowingRestore()).ApplyAsync(new[] { T("a", Reg("X", 0)) });
        Assert.Equal("nope", report.RestorePointWarning);
        Assert.Equal(1, report.Applied);
    }

    private sealed class ThrowingRestore : IRestorePointService
    {
        public Task<string?> CreateAsync(string description) => throw new InvalidOperationException("nope");
    }

    [Fact]
    public async Task Revert_engine_restores_everything_in_reverse()
    {
        var w = new World();
        w.Registry.SetValue("HKCU", "Software\\T", "X", "dword", 5);
        w.Services.Items["DiagTrack"] = new ServiceInfo("DiagTrack", StartType.Automatic, true);
        w.Tasks.Items["\\A"] = true;
        w.Appx.Add("Microsoft.BingNews");
        var store = new MemoryBackupStore();
        var tweak = T("all", Reg("X", 0), new ServiceAction { Name = "DiagTrack" },
            new ScheduledTaskAction { Path = "\\A" }, new AppxAction { PackageName = "Microsoft.BingNews" });
        await new ApplyEngine(w.Context, store, new FakeRestore()).ApplyAsync(new[] { tweak });

        var report = await new RevertEngine(w.Context).RevertAsync(store.Saved[0]);

        Assert.Equal(0, report.Failed);
        Assert.Equal(5, w.Registry.GetValue("HKCU", "Software\\T", "X")!.Value);
        Assert.Equal(StartType.Automatic, w.Services.Get("DiagTrack")!.StartType);
        Assert.True(w.Tasks.Items["\\A"]);
        Assert.Contains(report.Events, e => e.Result == StepResult.Skipped && e.Message.Contains("ms-windows-store://"));
    }

    [Fact]
    public async Task Scanner_aggregates_action_states()
    {
        var w = new World();
        w.Registry.SetValue("HKCU", "Software\\T", "A", "dword", 0);
        var applied = T("applied", Reg("A", 0));
        var partial = T("partial", Reg("A", 0), Reg("B", 0));
        var absent = T("absent", new ServiceAction { Name = "Nope" });
        var states = await Scanner.ScanAsync(new[] { applied, partial, absent }, w.Context);
        Assert.Equal(ActionState.Applied, states[0].State);
        Assert.Equal(ActionState.NotApplied, states[1].State);
        Assert.Equal(ActionState.NotPresent, states[2].State);
    }
}

public class BackupTests
{
    [Fact]
    public void Backup_json_round_trip_keeps_all_fields()
    {
        var file = new BackupFile { CreatedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), RestorePointWarning = "w" };
        file.Entries.Add(new BackupEntry
        {
            TweakId = "t", ActionKind = "registry", Target = "HKCU\\X", Hive = "HKCU", Path = "X", Name = "V",
            Existed = true, ValueKind = "dword", OldValue = "7",
        });
        file.Entries.Add(new BackupEntry { TweakId = "s", ActionKind = "service", Target = "DiagTrack", OldStartType = 2, WasRunning = true });
        file.Entries.Add(new BackupEntry
        {
            TweakId = "a", ActionKind = "appx", Target = "Foo",
            RemovedPackages = { new RemovedPackage { Name = "Foo", FamilyName = "Foo_abc" } },
        });

        var back = BackupStore.Deserialize(BackupStore.Serialize(file));

        Assert.Equal(file.CreatedUtc, back.CreatedUtc);
        Assert.Equal("w", back.RestorePointWarning);
        Assert.Equal(3, back.Entries.Count);
        Assert.Equal("7", back.Entries[0].OldValue);
        Assert.True(back.Entries[0].Existed);
        Assert.Equal(2, back.Entries[1].OldStartType);
        Assert.True(back.Entries[1].WasRunning);
        Assert.Equal("Foo_abc", back.Entries[2].RemovedPackages[0].FamilyName);
    }

    [Fact]
    public async Task Store_saves_lists_and_deletes_in_a_temp_folder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "limpio-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BackupStore(dir);
            var f = new BackupFile();
            f.Entries.Add(new BackupEntry { TweakId = "t", ActionKind = "scheduledTask", Target = "\\A", WasEnabled = true });
            var path = await store.SaveAsync(f);
            Assert.True(File.Exists(path));
            var list = await store.ListAsync();
            var one = Assert.Single(list);
            Assert.True(one.Entries[0].WasEnabled);
            await store.DeleteAsync(one);
            Assert.Empty(await store.ListAsync());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
