using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Actions;

public sealed class ServiceAction : TweakAction
{
    public string Name { get; set; } = "";
    /// <summary>Disabled, Manual or Automatic.</summary>
    public string StartType { get; set; } = "Disabled";

    public override string Kind => "service";
    public override string Target => Name;
    public override bool IsProtected => ProtectedList.IsProtectedService(Name);

    public StartType Desired => Enum.Parse<StartType>(StartType, true);

    public override Task<ActionState> DetectAsync(SystemContext ctx)
    {
        var s = ctx.Services.Get(Name);
        if (s is null) return Task.FromResult(ActionState.NotPresent);
        return Task.FromResult(s.StartType == Desired ? ActionState.Applied : ActionState.NotApplied);
    }

    public override Task<BackupEntry> ApplyAsync(SystemContext ctx)
    {
        var s = ctx.Services.Get(Name) ?? throw new InvalidOperationException($"Service {Name} not found");
        var e = NewEntry();
        e.OldStartType = (int)s.StartType;
        e.WasRunning = s.Running;
        ctx.Services.SetStartType(Name, Desired);
        if (Desired == Platform.StartType.Disabled && s.Running) ctx.Services.Stop(Name);
        return Task.FromResult(e);
    }

    public override Task RevertAsync(SystemContext ctx, BackupEntry entry)
    {
        if (entry.OldStartType is int t) ctx.Services.SetStartType(Name, (Platform.StartType)t);
        return Task.CompletedTask;
    }
}
