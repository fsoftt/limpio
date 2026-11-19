namespace Limpio.Core.Platform;

public sealed record RegValue(string Kind, object Value);

public interface IRegistryAccess
{
    RegValue? GetValue(string hive, string path, string name);
    void SetValue(string hive, string path, string name, string kind, object value);
    void DeleteValue(string hive, string path, string name);
}

public enum StartType { Boot = 0, System = 1, Automatic = 2, Manual = 3, Disabled = 4 }

public sealed record ServiceInfo(string Name, StartType StartType, bool Running);

public interface IServiceAccess
{
    ServiceInfo? Get(string name);
    void SetStartType(string name, StartType startType);
    void Stop(string name);
    IReadOnlyList<ServiceInfo> ListAll();
}

public sealed record TaskInfo(string Path, bool Enabled);

public interface ITaskAccess
{
    TaskInfo? Get(string path);
    void SetEnabled(string path, bool enabled);
    Task<int> CountEnabledAsync();
}

public sealed record AppxInfo(string Name, string FullName, string FamilyName, string? InstallLocation, bool IsFramework);

public interface IAppxAccess
{
    Task<IReadOnlyList<AppxInfo>> ListAsync(bool refresh = false);
    Task RemoveAsync(string packageName);
}

/// <summary>Access to classic Win32 apps: uninstall entries, processes and uninstaller execution.</summary>
public interface IWin32Access
{
    /// <summary>True when an uninstall entry (HKCU or HKLM) has this exact DisplayName.</summary>
    bool IsInstalled(string displayName);
    Task StopProcessAsync(string processName);
    /// <summary>Expands environment variables and wildcards in path segments; returns existing files.</summary>
    IReadOnlyList<string> FindFiles(string pattern);
    Task<int> RunAsync(string file, string args);
}

public interface IRestorePointService
{
    /// <summary>Creates a restore point. Returns null on success or a warning message.</summary>
    Task<string?> CreateAsync(string description);
}

public interface IStartupAccess
{
    int CountStartupEntries();
}

public interface IProcessInfo
{
    int ProcessCount();
    long RamUsedMb();
}

public interface IElevation
{
    bool IsElevated { get; }
}

/// <summary>Bundle of every system adapter, so actions can be faked in tests.</summary>
public sealed class SystemContext
{
    public required IRegistryAccess Registry { get; init; }
    public required IServiceAccess Services { get; init; }
    public required ITaskAccess Tasks { get; init; }
    public required IAppxAccess Appx { get; init; }
    public required IWin32Access Win32 { get; init; }
}
