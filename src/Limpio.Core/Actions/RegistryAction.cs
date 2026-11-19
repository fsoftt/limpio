using System.Globalization;
using System.Text.Json;
using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Actions;

public sealed class RegistryAction : TweakAction
{
    public string Hive { get; set; } = "HKCU";
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>dword, qword or string.</summary>
    public string ValueType { get; set; } = "dword";
    public JsonElement Value { get; set; }

    public override string Kind => "registry";
    public override string Target => Hive + "\\" + Path + "\\" + Name;
    public override bool IsProtected => false;

    public object DesiredValue => ValueType.ToLowerInvariant() switch
    {
        "dword" => (object)unchecked((int)(uint)Value.GetInt64()),
        "qword" => Value.GetInt64(),
        _ => Value.GetString() ?? "",
    };

    public override Task<ActionState> DetectAsync(SystemContext ctx)
    {
        var cur = ctx.Registry.GetValue(Hive, Path, Name);
        var applied = cur is not null && Equal(cur.Value, DesiredValue);
        return Task.FromResult(applied ? ActionState.Applied : ActionState.NotApplied);
    }

    public override Task<BackupEntry> ApplyAsync(SystemContext ctx)
    {
        var cur = ctx.Registry.GetValue(Hive, Path, Name);
        var e = NewEntry();
        e.Hive = Hive; e.Path = Path; e.Name = Name;
        e.Existed = cur is not null;
        if (cur is not null)
        {
            e.ValueKind = cur.Kind;
            e.OldValue = Convert.ToString(cur.Value, CultureInfo.InvariantCulture);
        }
        ctx.Registry.SetValue(Hive, Path, Name, ValueType.ToLowerInvariant(), DesiredValue);
        return Task.FromResult(e);
    }

    public override Task RevertAsync(SystemContext ctx, BackupEntry entry)
    {
        if (!entry.Existed)
            ctx.Registry.DeleteValue(entry.Hive!, entry.Path!, entry.Name!);
        else
        {
            object v = entry.ValueKind switch
            {
                "dword" => unchecked((int)(uint)long.Parse(entry.OldValue!, CultureInfo.InvariantCulture)),
                "qword" => long.Parse(entry.OldValue!, CultureInfo.InvariantCulture),
                _ => entry.OldValue ?? "",
            };
            ctx.Registry.SetValue(entry.Hive!, entry.Path!, entry.Name!, entry.ValueKind!, v);
        }
        return Task.CompletedTask;
    }

    private static bool Equal(object a, object b)
    {
        if (a is string sa && b is string sb) return string.Equals(sa, sb, StringComparison.Ordinal);
        try { return Convert.ToInt64(a, CultureInfo.InvariantCulture) == Convert.ToInt64(b, CultureInfo.InvariantCulture); }
        catch { return false; }
    }
}
