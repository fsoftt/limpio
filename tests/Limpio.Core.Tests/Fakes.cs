using Limpio.Core.Backup;
using Limpio.Core.Platform;

namespace Limpio.Core.Tests;

public sealed class FakeRegistry : IRegistryAccess
{
    public Dictionary<string, RegValue> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    private static string K(string h, string p, string n) => $"{h}|{p}|{n}";

    public RegValue? GetValue(string hive, string path, string name) =>
        Values.TryGetValue(K(hive, path, name), out var v) ? v : null;

    /// <summary>Value names whose writes are refused, like values guarded by UCPD.</summary>
    public HashSet<string> DenyWrite { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Exception DenyWith { get; set; } = new UnauthorizedAccessException("Attempted to perform an unauthorized operation.");

    public void SetValue(string hive, string path, string name, string kind, object value)
    {
        if (DenyWrite.Contains(name)) throw DenyWith;
        Values[K(hive, path, name)] = new RegValue(kind, value);
    }

    public void DeleteValue(string hive, string path, string name) => Values.Remove(K(hive, path, name));
}

public sealed class FakeServices : IServiceAccess
{
    public Dictionary<string, ServiceInfo> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Log { get; } = new();

    public ServiceInfo? Get(string name) => Items.TryGetValue(name, out var s) ? s : null;

    public void SetStartType(string name, StartType startType)
    {
        Log.Add($"config {name} {startType}");
        Items[name] = Items[name] with { StartType = startType };
    }

    public void Stop(string name)
    {
        Log.Add("stop " + name);
        Items[name] = Items[name] with { Running = false };
    }

    public IReadOnlyList<ServiceInfo> ListAll() => Items.Values.ToList();
}

public sealed class FakeTasks : ITaskAccess
{
    public Dictionary<string, bool> Items { get; } = new(StringComparer.OrdinalIgnoreCase);

    public TaskInfo? Get(string path) => Items.TryGetValue(path, out var e) ? new TaskInfo(path, e) : null;
    public void SetEnabled(string path, bool enabled) => Items[path] = enabled;
    public Task<int> CountEnabledAsync() => Task.FromResult(Items.Values.Count(v => v));
}

public sealed class FakeAppx : IAppxAccess
{
    public List<AppxInfo> Items { get; } = new();
    public List<string> Removed { get; } = new();
    public HashSet<string> FailOn { get; } = new();

    public Task<IReadOnlyList<AppxInfo>> ListAsync(bool refresh = false) =>
        Task.FromResult<IReadOnlyList<AppxInfo>>(Items.ToList());

    public Task RemoveAsync(string packageName)
    {
        if (FailOn.Contains(packageName)) throw new InvalidOperationException("boom " + packageName);
        Removed.Add(packageName);
        Items.RemoveAll(i => i.Name == packageName);
        return Task.CompletedTask;
    }

    public FakeAppx Add(string name, bool framework = false)
    {
        Items.Add(new AppxInfo(name, name + "_1.0_x64__abc", name + "_abc", null, framework));
        return this;
    }
}

public sealed class FakeWin32 : IWin32Access
{
    public HashSet<string> Installed { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Log { get; } = new();
    public int ExitCode { get; set; }

    public bool IsInstalled(string displayName) => Installed.Contains(displayName);
    public Task StopProcessAsync(string processName) { Log.Add("kill " + processName); return Task.CompletedTask; }
    public IReadOnlyList<string> FindFiles(string pattern) => Files.Contains(pattern) ? new[] { pattern } : Array.Empty<string>();

    public Task<int> RunAsync(string file, string args)
    {
        Log.Add($"run {file} {args}");
        if (ExitCode == 0) Installed.Clear();
        return Task.FromResult(ExitCode);
    }
}

public sealed class FakeRestore : IRestorePointService
{
    public string? Result { get; set; }
    public int Calls { get; private set; }
    public bool? ProtectionEnabled { get; set; }
    public string? EnableResult { get; set; }
    public int EnableCalls { get; private set; }
    public Task<string?> CreateAsync(string description) { Calls++; return Task.FromResult(Result); }
    public Task<bool?> IsProtectionEnabledAsync() => Task.FromResult(ProtectionEnabled);
    public Task<string?> EnableProtectionAsync() { EnableCalls++; return Task.FromResult(EnableResult); }
}

public sealed class MemoryBackupStore : IBackupStore
{
    public List<BackupFile> Saved { get; } = new();

    public Task<string> SaveAsync(BackupFile backup) { Saved.Add(backup); return Task.FromResult("memory"); }
    public Task<IReadOnlyList<BackupFile>> ListAsync() => Task.FromResult<IReadOnlyList<BackupFile>>(Saved);
    public Task DeleteAsync(BackupFile backup) { Saved.Remove(backup); return Task.CompletedTask; }
}

public sealed class FakeProc : IProcessInfo
{
    public int Processes { get; set; } = 100;
    public long Ram { get; set; } = 4000;
    public int ProcessCount() => Processes;
    public long RamUsedMb() => Ram;
}

public sealed class FakeStartup : IStartupAccess
{
    public int Count { get; set; } = 5;
    public int CountStartupEntries() => Count;
}

public sealed class World
{
    public FakeRegistry Registry { get; } = new();
    public FakeServices Services { get; } = new();
    public FakeTasks Tasks { get; } = new();
    public FakeAppx Appx { get; } = new();
    public FakeWin32 Win32 { get; } = new();

    public SystemContext Context => new()
    {
        Registry = Registry, Services = Services, Tasks = Tasks, Appx = Appx, Win32 = Win32,
    };
}
