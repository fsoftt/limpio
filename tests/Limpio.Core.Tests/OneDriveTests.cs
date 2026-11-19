using System.Text.Json;
using Limpio.Core.Actions;
using Limpio.Core.Catalog;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Tests;

public class OneDriveTests
{
    private const string Usf = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\User Shell Folders";
    private const string Setup = "%SystemRoot%\\System32\\OneDriveSetup.exe";

    private static Win32UninstallAction Action() => new()
    {
        DisplayName = "Microsoft OneDrive", ProcessName = "OneDrive", Uninstallers = { Setup },
        RunValueName = "OneDrive", ProtectKnownFolders = true, RevertHint = "winget install Microsoft.OneDrive",
    };

    private static World Installed()
    {
        var w = new World();
        w.Win32.Installed.Add("Microsoft OneDrive");
        w.Win32.Files.Add(Setup);
        return w;
    }

    [Theory]
    [InlineData("%USERPROFILE%\\OneDrive\\Desktop", true)]
    [InlineData("C:\\Users\\a\\OneDrive - Contoso\\Documents", true)]
    [InlineData("%OneDrive%\\Pictures", true)]
    [InlineData("%USERPROFILE%\\Desktop", false)]
    [InlineData("D:\\Docs", false)]
    public void Path_inside_onedrive_detection(string path, bool expected) =>
        Assert.Equal(expected, OneDriveGuard.IsInsideOneDrive(path));

    [Fact]
    public void Guard_lists_only_redirected_folders()
    {
        var w = new World();
        w.Registry.SetValue("HKCU", Usf, "Desktop", "string", "%USERPROFILE%\\OneDrive\\Desktop");
        w.Registry.SetValue("HKCU", Usf, "Personal", "string", "%USERPROFILE%\\Documents");
        w.Registry.SetValue("HKCU", Usf, "My Pictures", "string", "%USERPROFILE%\\OneDrive\\Pictures");
        Assert.Equal(new[] { "Desktop", "My Pictures" }, OneDriveGuard.RedirectedFolders(w.Registry));
    }

    [Fact]
    public void Guard_is_empty_when_nothing_is_redirected()
    {
        Assert.Empty(OneDriveGuard.RedirectedFolders(new World().Registry));
    }

    [Fact]
    public async Task Apply_is_refused_when_known_folders_are_in_onedrive_and_nothing_runs()
    {
        var w = Installed();
        w.Registry.SetValue("HKCU", Usf, "Personal", "string", "%USERPROFILE%\\OneDrive\\Documents");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Action().ApplyAsync(w.Context));
        Assert.Contains("Personal", ex.Message);
        Assert.Empty(w.Win32.Log);
    }

    [Fact]
    public async Task Apply_stops_process_runs_uninstaller_and_removes_run_entry()
    {
        var w = Installed();
        w.Registry.SetValue("HKCU", Win32UninstallAction.RunKey, "OneDrive", "string", "\"OneDrive.exe\" /background");
        var a = Action();
        Assert.Equal(ActionState.NotApplied, await a.DetectAsync(w.Context));

        var entry = await a.ApplyAsync(w.Context);

        Assert.Equal(new[] { "kill OneDrive", "run " + Setup + " /uninstall" }, w.Win32.Log);
        Assert.Null(w.Registry.GetValue("HKCU", Win32UninstallAction.RunKey, "OneDrive"));
        Assert.True(entry.Existed);
        Assert.Equal("winget install Microsoft.OneDrive", entry.RevertHint);
        Assert.Equal(ActionState.NotPresent, await a.DetectAsync(w.Context));
    }

    [Fact]
    public async Task Missing_uninstaller_or_failing_exit_code_throws()
    {
        var w = Installed();
        w.Win32.Files.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Action().ApplyAsync(w.Context));
        w.Win32.Files.Add(Setup);
        w.Win32.ExitCode = 5;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Action().ApplyAsync(w.Context));
    }

    [Fact]
    public void Catalog_has_onedrive_as_moderate_aggressive_only_apps_tweak()
    {
        var t = TweakCatalog.LoadEmbedded().Find("apps.onedrive")!;
        Assert.Equal(Category.Apps, t.Category);
        Assert.Equal(Risk.Moderate, t.Risk);
        Assert.Equal(new[] { Preset.Aggressive }, t.Presets);
        var a = Assert.IsType<Win32UninstallAction>(Assert.Single(t.Actions));
        Assert.True(a.ProtectKnownFolders);
        Assert.Contains("winget install Microsoft.OneDrive", a.RevertHint);
    }

    [Fact]
    public void Aggressive_preset_includes_xbox_gamebar_phonelink_and_widgets()
    {
        var c = TweakCatalog.LoadEmbedded();
        var agg = PresetSelector.Select(c, Preset.Aggressive);
        foreach (var id in new[] { "apps.xbox", "services.xbox", "gaming.gamebar", "apps.phone-link", "apps.widgets" })
            Assert.Contains(id, agg);
        var all = c.Tweaks.SelectMany(t => t.Actions).ToList();
        Assert.Contains(all.OfType<ServiceAction>(), s => s.Name == "XblAuthManager");
        Assert.Contains(all.OfType<ServiceAction>(), s => s.Name == "XblGameSave");
        Assert.Contains(all.OfType<ServiceAction>(), s => s.Name == "XboxNetApiSvc");
        Assert.Contains(all.OfType<RegistryAction>(), r => r.Name == "AppCaptureEnabled");
        Assert.Contains(all.OfType<RegistryAction>(), r => r.Name == "GameDVR_Enabled");
        Assert.Contains(all.OfType<RegistryAction>(), r => r.Name == "AllowNewsAndInterests");
        Assert.Contains(all.OfType<AppxAction>(), a => a.PackageName == "MicrosoftWindows.CrossDevice");
        Assert.Contains(all.OfType<AppxAction>(), a => a.PackageName == "MicrosoftWindows.Client.WebExperience");
    }
}
