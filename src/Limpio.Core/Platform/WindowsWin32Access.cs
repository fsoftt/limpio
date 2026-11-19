using System.Diagnostics;
using Microsoft.Win32;

namespace Limpio.Core.Platform;

public sealed class WindowsWin32Access : IWin32Access
{
    private static readonly (RegistryHive Hive, string Path)[] UninstallRoots =
    {
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
    };

    public bool IsInstalled(string displayName)
    {
        foreach (var (hive, path) in UninstallRoots)
        {
            using var b = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var root = b.OpenSubKey(path);
            if (root is null) continue;
            foreach (var name in root.GetSubKeyNames())
            {
                using var k = root.OpenSubKey(name);
                if (string.Equals(k?.GetValue("DisplayName") as string, displayName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    public async Task StopProcessAsync(string processName)
    {
        foreach (var p in Process.GetProcessesByName(processName))
        {
            try { p.Kill(true); await p.WaitForExitAsync(); }
            catch { /* already gone */ }
            finally { p.Dispose(); }
        }
    }

    public IReadOnlyList<string> FindFiles(string pattern)
    {
        var parts = Environment.ExpandEnvironmentVariables(pattern).Split('\\');
        var current = new List<string> { parts[0] + "\\" };
        for (var i = 1; i < parts.Length; i++)
        {
            var next = new List<string>();
            foreach (var c in current)
            {
                try
                {
                    if (parts[i].Contains('*'))
                        next.AddRange(i == parts.Length - 1 ? Directory.GetFiles(c, parts[i]) : Directory.GetDirectories(c, parts[i]));
                    else next.Add(Path.Combine(c, parts[i]));
                }
                catch { /* missing or inaccessible */ }
            }
            current = next;
        }
        return current.Where(File.Exists).ToList();
    }

    public async Task<int> RunAsync(string file, string args)
    {
        var (code, _, _) = await ProcRunner.RunAsync(file, args);
        return code;
    }
}
