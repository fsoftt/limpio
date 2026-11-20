using System.Text.Json.Serialization;

namespace Limpio.Core.Backup;

public sealed class RemovedPackage
{
    public string Name { get; set; } = "";
    public string FamilyName { get; set; } = "";

    /// <summary>Deep link that opens the Microsoft Store page of the package.</summary>
    [JsonIgnore]
    public string StoreLink => $"ms-windows-store://pdp/?PFN={FamilyName}";
}

/// <summary>Original state of one action's target, captured right before it was changed.</summary>
public sealed class BackupEntry
{
    public string TweakId { get; set; } = "";
    public string ActionKind { get; set; } = "";
    public string Target { get; set; } = "";

    // registry
    public string? Hive { get; set; }
    public string? Path { get; set; }
    public string? Name { get; set; }
    public bool Existed { get; set; }
    public string? ValueKind { get; set; }
    public string? OldValue { get; set; }

    // service
    public int? OldStartType { get; set; }
    public bool WasRunning { get; set; }

    // scheduled task
    public bool WasEnabled { get; set; }

    // win32 uninstall: how to get the app back
    public string? RevertHint { get; set; }

    // appx (cannot be restored automatically)
    public List<RemovedPackage> RemovedPackages { get; set; } = new();
}

public sealed class BackupFile
{
    public int Version { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Machine { get; set; } = Environment.MachineName;
    public string? RestorePointWarning { get; set; }
    public List<BackupEntry> Entries { get; set; } = new();

    /// <summary>File path on disk, filled when listed or saved. Not serialized.</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }
}
