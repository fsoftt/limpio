using System.Text.Json.Serialization;
using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Actions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AppxAction), "appx")]
[JsonDerivedType(typeof(ServiceAction), "service")]
[JsonDerivedType(typeof(ScheduledTaskAction), "scheduledTask")]
[JsonDerivedType(typeof(RegistryAction), "registry")]
[JsonDerivedType(typeof(Win32UninstallAction), "win32Uninstall")]
public abstract class TweakAction
{
    [JsonIgnore] public abstract string Kind { get; }
    /// <summary>Human readable target (package, service, task path or registry value).</summary>
    [JsonIgnore] public abstract string Target { get; }
    /// <summary>True when the action points at something on the protected list.</summary>
    [JsonIgnore] public abstract bool IsProtected { get; }

    public abstract Task<ActionState> DetectAsync(SystemContext ctx);

    /// <summary>Applies the change and returns the original state for revert.</summary>
    public abstract Task<BackupEntry> ApplyAsync(SystemContext ctx);

    public abstract Task RevertAsync(SystemContext ctx, BackupEntry entry);

    protected BackupEntry NewEntry() => new() { ActionKind = Kind, Target = Target };
}
