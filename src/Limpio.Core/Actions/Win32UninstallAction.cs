using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Actions;

/// <summary>
/// Uninstalls a classic Win32 app (for example OneDrive). Detection uses the uninstall registry keys.
/// </summary>
public sealed class Win32UninstallAction : TweakAction
{
    /// <summary>Exact DisplayName in the uninstall keys, for example "Microsoft OneDrive".</summary>
    public string DisplayName { get; set; } = "";
    public string? ProcessName { get; set; }
    /// <summary>Candidate uninstaller paths (env vars and wildcards allowed); the first existing one runs.</summary>
    public List<string> Uninstallers { get; set; } = new();
    public string UninstallArgs { get; set; } = "/uninstall";
    /// <summary>Value name under HKCU Run to remove after uninstalling.</summary>
    public string? RunValueName { get; set; }
    /// <summary>Refuse when Desktop, Documents or Pictures are redirected inside OneDrive.</summary>
    public bool ProtectKnownFolders { get; set; }
    public string? RevertHint { get; set; }

    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public override string Kind => "win32Uninstall";
    public override string Target => DisplayName;
    public override bool IsProtected => false;

    public override Task<ActionState> DetectAsync(SystemContext ctx) =>
        Task.FromResult(ctx.Win32.IsInstalled(DisplayName) ? ActionState.NotApplied : ActionState.NotPresent);

    public override async Task<BackupEntry> ApplyAsync(SystemContext ctx)
    {
        if (ProtectKnownFolders)
        {
            var redirected = OneDriveGuard.RedirectedFolders(ctx.Registry);
            if (redirected.Count > 0)
                throw new InvalidOperationException(
                    "Refused: these folders live inside OneDrive: " + string.Join(", ", redirected)
                    + ". Move them back to a local folder first (your files are not touched).");
        }

        var uninstaller = Uninstallers.SelectMany(ctx.Win32.FindFiles).FirstOrDefault()
                          ?? throw new InvalidOperationException("Uninstaller not found for " + DisplayName);

        var e = NewEntry();
        e.RevertHint = RevertHint;
        if (RunValueName is not null)
        {
            var run = ctx.Registry.GetValue("HKCU", RunKey, RunValueName);
            e.Hive = "HKCU"; e.Path = RunKey; e.Name = RunValueName;
            e.Existed = run is not null;
            if (run is not null) { e.ValueKind = run.Kind; e.OldValue = Convert.ToString(run.Value); }
        }

        if (ProcessName is not null) await ctx.Win32.StopProcessAsync(ProcessName);
        var code = await ctx.Win32.RunAsync(uninstaller, UninstallArgs);
        if (code != 0) throw new InvalidOperationException($"Uninstaller exited with code {code}");
        if (RunValueName is not null) ctx.Registry.DeleteValue("HKCU", RunKey, RunValueName);
        return e;
    }

    /// <summary>The app cannot be reinstalled automatically; the hint tells the user how.</summary>
    public override Task RevertAsync(SystemContext ctx, BackupEntry entry) => Task.CompletedTask;
}

/// <summary>Safety check: never uninstall OneDrive while user folders are redirected into it.</summary>
public static class OneDriveGuard
{
    private const string UserShellFolders = @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";

    /// <summary>Value names checked: Desktop, Personal (Documents) and My Pictures.</summary>
    public static readonly string[] FolderValues = { "Desktop", "Personal", "My Pictures" };

    public static IReadOnlyList<string> RedirectedFolders(IRegistryAccess registry)
    {
        var result = new List<string>();
        foreach (var name in FolderValues)
        {
            if (registry.GetValue("HKCU", UserShellFolders, name)?.Value is string path && IsInsideOneDrive(path))
                result.Add(name);
        }
        return result;
    }

    public static bool IsInsideOneDrive(string path) =>
        path.Split('\\', '/').Any(seg => seg.Contains("OneDrive", StringComparison.OrdinalIgnoreCase));
}
