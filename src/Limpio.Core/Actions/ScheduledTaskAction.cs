using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Actions;

public sealed class ScheduledTaskAction : TweakAction
{
    public string Path { get; set; } = "";

    public override string Kind => "scheduledTask";
    public override string Target => Path;
    public override bool IsProtected => false;

    public override Task<ActionState> DetectAsync(SystemContext ctx)
    {
        var t = ctx.Tasks.Get(Path);
        if (t is null) return Task.FromResult(ActionState.NotPresent);
        return Task.FromResult(t.Enabled ? ActionState.NotApplied : ActionState.Applied);
    }

    public override Task<BackupEntry> ApplyAsync(SystemContext ctx)
    {
        var t = ctx.Tasks.Get(Path) ?? throw new InvalidOperationException($"Task {Path} not found");
        var e = NewEntry();
        e.WasEnabled = t.Enabled;
        ctx.Tasks.SetEnabled(Path, false);
        return Task.FromResult(e);
    }

    public override Task RevertAsync(SystemContext ctx, BackupEntry entry)
    {
        if (entry.WasEnabled) ctx.Tasks.SetEnabled(Path, true);
        return Task.CompletedTask;
    }
}
