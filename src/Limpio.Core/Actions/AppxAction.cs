using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Actions;

public sealed class AppxAction : TweakAction
{
    /// <summary>Package name; '*' wildcards are allowed (for example "*McAfee*").</summary>
    public string PackageName { get; set; } = "";

    public override string Kind => "appx";
    public override string Target => PackageName;
    public override bool IsProtected => ProtectedList.IsProtectedPackage(PackageName);

    private static IEnumerable<AppxInfo> Matches(IEnumerable<AppxInfo> all, string pattern) =>
        all.Where(p => !p.IsFramework
                       && ProtectedList.WildcardMatch(pattern, p.Name)
                       && !ProtectedList.IsProtectedPackage(p.Name));

    public override async Task<ActionState> DetectAsync(SystemContext ctx)
    {
        var all = await ctx.Appx.ListAsync();
        return Matches(all, PackageName).Any() ? ActionState.NotApplied : ActionState.NotPresent;
    }

    public override async Task<BackupEntry> ApplyAsync(SystemContext ctx)
    {
        var all = await ctx.Appx.ListAsync();
        var e = NewEntry();
        foreach (var p in Matches(all, PackageName).ToList())
        {
            await ctx.Appx.RemoveAsync(p.Name);
            e.RemovedPackages.Add(new RemovedPackage { Name = p.Name, FamilyName = p.FamilyName });
        }
        return e;
    }

    /// <summary>Removed apps cannot be restored automatically; the user reinstalls them from the Store.</summary>
    public override Task RevertAsync(SystemContext ctx, BackupEntry entry) => Task.CompletedTask;
}
