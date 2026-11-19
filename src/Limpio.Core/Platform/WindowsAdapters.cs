using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Limpio.Core.Engine;
using Microsoft.Win32;

namespace Limpio.Core.Platform;

internal static class ProcRunner
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start " + file);
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, await o, await e);
    }

    public static Task<(int ExitCode, string Output, string Error)> PowerShellAsync(string script)
    {
        var full = "[Console]::OutputEncoding=[Text.Encoding]::UTF8; $ProgressPreference='SilentlyContinue'; " + script;
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
        return RunAsync("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc);
    }

    public static string Sync(string file, string args)
    {
        var (code, o, e) = RunAsync(file, args).GetAwaiter().GetResult();
        if (code != 0) throw new InvalidOperationException($"{file} {args}: exit {code} {e}{o}".Trim());
        return o;
    }
}

public sealed class WindowsElevation : IElevation
{
    public bool IsElevated
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}

public sealed class WindowsRegistryAccess : IRegistryAccess
{
    private static RegistryKey Base(string hive) => RegistryKey.OpenBaseKey(
        hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase) ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
        RegistryView.Registry64);

    public RegValue? GetValue(string hive, string path, string name)
    {
        using var b = Base(hive);
        using var k = b.OpenSubKey(path);
        var v = k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (v is null) return null;
        var kind = k!.GetValueKind(name);
        return kind switch
        {
            RegistryValueKind.DWord => new RegValue("dword", (int)v),
            RegistryValueKind.QWord => new RegValue("qword", (long)v),
            _ => new RegValue("string", v.ToString() ?? ""),
        };
    }

    public void SetValue(string hive, string path, string name, string kind, object value)
    {
        using var b = Base(hive);
        using var k = b.CreateSubKey(path, writable: true);
        var rk = kind switch { "dword" => RegistryValueKind.DWord, "qword" => RegistryValueKind.QWord, _ => RegistryValueKind.String };
        k.SetValue(name, value, rk);
    }

    public void DeleteValue(string hive, string path, string name)
    {
        using var b = Base(hive);
        using var k = b.OpenSubKey(path, writable: true);
        k?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class WindowsServiceAccess : IServiceAccess
{
    public ServiceInfo? Get(string name)
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name);
        if (k?.GetValue("Start") is not int start) return null;
        return new ServiceInfo(name, (StartType)start, IsRunning(name));
    }

    private static bool IsRunning(string name)
    {
        try
        {
            using var s = new ManagementObjectSearcher(
                $"SELECT State FROM Win32_Service WHERE Name='{name.Replace("'", "")}'");
            foreach (ManagementBaseObject o in s.Get()) return (o["State"]?.ToString() ?? "") == "Running";
        }
        catch { /* WMI unavailable */ }
        return false;
    }

    public IReadOnlyList<ServiceInfo> ListAll()
    {
        var list = new List<ServiceInfo>();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name,State,StartMode FROM Win32_Service");
            foreach (ManagementBaseObject o in s.Get())
            {
                var mode = (o["StartMode"]?.ToString() ?? "") switch
                {
                    "Auto" => StartType.Automatic, "Manual" => StartType.Manual, "Disabled" => StartType.Disabled,
                    "Boot" => StartType.Boot, _ => StartType.System,
                };
                list.Add(new ServiceInfo(o["Name"]?.ToString() ?? "", mode, o["State"]?.ToString() == "Running"));
            }
        }
        catch { /* WMI unavailable */ }
        return list;
    }

    public void SetStartType(string name, StartType startType)
    {
        var mode = startType switch
        {
            StartType.Disabled => "disabled", StartType.Manual => "demand", StartType.Automatic => "auto",
            StartType.Boot => "boot", _ => "system",
        };
        ProcRunner.Sync("sc.exe", $"config \"{name}\" start= {mode}");
    }

    public void Stop(string name)
    {
        try { ProcRunner.Sync("sc.exe", $"stop \"{name}\""); }
        catch { /* already stopped or cannot stop: start type change is what matters */ }
    }
}

public sealed partial class WindowsTaskAccess : ITaskAccess
{
    public TaskInfo? Get(string path)
    {
        var (code, output, _) = ProcRunner.RunAsync("schtasks.exe", $"/Query /TN \"{path}\" /XML").GetAwaiter().GetResult();
        if (code != 0) return null;
        var disabled = EnabledFalse().IsMatch(output);
        return new TaskInfo(path, !disabled);
    }

    public void SetEnabled(string path, bool enabled) =>
        ProcRunner.Sync("schtasks.exe", $"/Change /TN \"{path}\" {(enabled ? "/Enable" : "/Disable")}");

    public async Task<int> CountEnabledAsync()
    {
        var (code, o, _) = await ProcRunner.PowerShellAsync(
            "(Get-ScheduledTask | Where-Object { $_.State -ne 'Disabled' } | Measure-Object).Count");
        return code == 0 && int.TryParse(o.Trim(), out var n) ? n : 0;
    }

    [GeneratedRegex(@"<Enabled>\s*false\s*</Enabled>", RegexOptions.IgnoreCase)]
    private static partial Regex EnabledFalse();
}

public sealed partial class WindowsAppxAccess : IAppxAccess
{
    private readonly bool _elevated;
    private IReadOnlyList<AppxInfo>? _cache;

    public WindowsAppxAccess(bool elevated) => _elevated = elevated;

    public async Task<IReadOnlyList<AppxInfo>> ListAsync(bool refresh = false)
    {
        if (_cache is not null && !refresh) return _cache;
        var all = _elevated ? "-AllUsers" : "";
        var (code, o, _) = await ProcRunner.PowerShellAsync(
            $"Get-AppxPackage {all} | Select-Object Name,PackageFullName,PackageFamilyName,InstallLocation,IsFramework | ConvertTo-Json -Compress");
        var list = new List<AppxInfo>();
        if (code == 0 && !string.IsNullOrWhiteSpace(o))
        {
            using var doc = JsonDocument.Parse(o);
            var items = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };
            foreach (var e in items)
            {
                list.Add(new AppxInfo(
                    e.GetProperty("Name").GetString() ?? "",
                    e.GetProperty("PackageFullName").GetString() ?? "",
                    e.GetProperty("PackageFamilyName").GetString() ?? "",
                    e.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null,
                    e.TryGetProperty("IsFramework", out var fw) && fw.ValueKind == JsonValueKind.True));
            }
        }
        _cache = list.GroupBy(p => p.Name).Select(g => g.First()).ToList();
        return _cache;
    }

    public async Task RemoveAsync(string packageName)
    {
        if (!SafeName().IsMatch(packageName)) throw new ArgumentException("Unsafe package name: " + packageName);
        var script =
            $"$n='{packageName}'; " +
            "Get-AppxPackage -AllUsers -Name $n | Remove-AppxPackage -AllUsers -ErrorAction Stop; " +
            "Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $n } | Remove-AppxProvisionedPackage -Online -ErrorAction Stop | Out-Null";
        var (code, o, e) = await ProcRunner.PowerShellAsync(script);
        if (code != 0) throw new InvalidOperationException((e + o).Trim());
        _cache = null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9.\-_]+$")]
    private static partial Regex SafeName();
}

public sealed class WindowsRestorePointService : IRestorePointService
{
    private const string SrKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string FreqName = "SystemRestorePointCreationFrequency";
    private readonly string _lang;

    public WindowsRestorePointService(string language = "en") => _lang = language;

    public static string SystemDrive => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mountPoint, StringBuilder volumeName, uint len);

    public Task<bool?> IsProtectionEnabledAsync() => Task.Run<bool?>(() =>
    {
        try
        {
            using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using (var pol = b.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore"))
                if (pol?.GetValue("DisableSR") is int d && d == 1) return false;
            using var clients = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients");
            if (clients?.GetValue("{09F7EDC5-294E-4180-AF6A-FB0E6A0E9513}") is not string[] entries) return null;
            var sb = new StringBuilder(64);
            if (!GetVolumeNameForVolumeMountPoint(SystemDrive, sb, (uint)sb.Capacity)) return null;
            var vol = sb.ToString().TrimEnd('\\');
            // Each entry holds a volume path such as 1:\\?\Volume{guid}\ (1 = protection on for that volume).
            return entries.Any(e => e.StartsWith("1:", StringComparison.Ordinal)
                                    && e.Contains(vol, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    });

    public async Task<string?> EnableProtectionAsync()
    {
        var drive = SystemDrive;
        var (code, o, e) = await ProcRunner.PowerShellAsync($"Enable-ComputerRestore -Drive '{drive}' -ErrorAction Stop");
        if (code == 0) return null;
        var msg = (e + o).Trim();
        var text = RestorePointReasons.Disabled(drive.TrimEnd('\\')).Get(_lang);
        return string.IsNullOrEmpty(msg) ? text : text + " " + msg;
    }

    public Task<string?> CreateAsync(string description) => Task.Run<string?>(() =>
    {
        var drive = SystemDrive.TrimEnd('\\');
        try
        {
            using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            if (IsProtectionEnabledAsync().GetAwaiter().GetResult() == false)
                return RestorePointReasons.Disabled(drive).Get(_lang);

            // Windows skips the restore point silently when one was made in the last 24 hours.
            // Lift that limit for this call and put the original value back afterwards.
            using var key = b.CreateSubKey(SrKey, writable: true);
            var original = key.GetValue(FreqName);
            var originalKind = original is null ? RegistryValueKind.DWord : key.GetValueKind(FreqName);
            try
            {
                key.SetValue(FreqName, 0, RegistryValueKind.DWord);
                using var cls = new ManagementClass(@"\\.\root\default:SystemRestore");
                var args = cls.GetMethodParameters("CreateRestorePoint");
                args["Description"] = description;
                args["RestorePointType"] = 12; // MODIFY_SETTINGS
                args["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
                var res = cls.InvokeMethod("CreateRestorePoint", args, null);
                var code = Convert.ToUInt32(res?["ReturnValue"] ?? 0u);
                return code == 0 ? null : RestorePointReasons.FromReturnCode(code).Get(_lang);
            }
            finally
            {
                if (original is null) key.DeleteValue(FreqName, throwOnMissingValue: false);
                else key.SetValue(FreqName, original, originalKind);
            }
        }
        catch (Exception ex)
        {
            return RestorePointReasons.FromException(ex).Get(_lang);
        }
    });
}

public sealed class WindowsStartupAccess : IStartupAccess
{
    private static readonly string[] RunPaths =
    {
        @"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run",
    };

    public int CountStartupEntries()
    {
        var n = 0;
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            foreach (var p in RunPaths)
            {
                using var run = hive.OpenSubKey(p);
                if (run is null) continue;
                using var approved = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                foreach (var v in run.GetValueNames())
                    if (!IsDisabled(approved, v)) n++;
            }
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                 })
        {
            if (!Directory.Exists(folder)) continue;
            n += Directory.GetFiles(folder).Count(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase));
        }
        return n;
    }

    private static bool IsDisabled(RegistryKey? approved, string name) =>
        approved?.GetValue(name) is byte[] { Length: > 0 } b && (b[0] & 1) == 1;
}

public sealed class WindowsProcessInfo : IProcessInfo
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    public int ProcessCount()
    {
        var procs = Process.GetProcesses();
        var n = procs.Length;
        foreach (var p in procs) p.Dispose();
        return n;
    }

    public long RamUsedMb()
    {
        var m = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref m) ? (long)((m.ullTotalPhys - m.ullAvailPhys) / (1024 * 1024)) : 0;
    }
}

public static class WindowsSystem
{
    public static SystemContext CreateContext(bool elevated) => new()
    {
        Registry = new WindowsRegistryAccess(),
        Services = new WindowsServiceAccess(),
        Tasks = new WindowsTaskAccess(),
        Appx = new WindowsAppxAccess(elevated),
        Win32 = new WindowsWin32Access(),
    };

    /// <summary>Best-effort recursive folder size in bytes.</summary>
    public static long FolderSizeBytes(string path)
    {
        long total = 0;
        try
        {
            foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", new System.IO.EnumerationOptions
                     { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                total += f.Length;
        }
        catch { /* best effort */ }
        return total;
    }
}
