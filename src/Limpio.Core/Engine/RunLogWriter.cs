using System.Text;

namespace Limpio.Core.Engine;

/// <summary>
/// Writes the log of one run to <c>%LOCALAPPDATA%\Limpio\logs\yyyyMMdd-HHmmss.log</c>.
/// Lines are appended as they happen, so a crash in the middle still leaves a log.
/// </summary>
public sealed class RunLogWriter
{
    private readonly object _gate = new();

    public string Path { get; }

    public RunLogWriter(DateTime? now = null, string? folder = null)
    {
        folder ??= AppPaths.Logs;
        Directory.CreateDirectory(folder);
        var stamp = (now ?? DateTime.Now).ToString("yyyyMMdd-HHmmss");
        var path = System.IO.Path.Combine(folder, stamp + ".log");
        for (var i = 2; File.Exists(path); i++)
            path = System.IO.Path.Combine(folder, $"{stamp}-{i}.log");
        Path = path;
    }

    public void Line(string text)
    {
        lock (_gate)
            File.AppendAllText(Path, text + Environment.NewLine, new UTF8Encoding(false));
    }

    /// <summary>Formats one event as a single log line (multi-line messages are kept on one line).</summary>
    public static string Format(ApplyEvent e, DateTime? at = null)
    {
        var msg = (e.Message ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        var ucpd = e.UcpdActive ? " [UCPD running]" : "";
        return $"{(at ?? DateTime.Now):HH:mm:ss} [{e.Done}/{e.Total}] {e.Result,-9} {e.TweakId} | {e.Target} | {msg}{ucpd}";
    }

    public void Event(ApplyEvent e) => Line(Format(e));

    public void Summary(ApplyReport report)
    {
        Line($"Applied={report.Applied} Blocked={report.Blocked} Failed={report.Failed}");
        foreach (var (id, outcome) in report.Outcomes()) Line($"tweak {id}: {outcome}");
        if (report.BackupPath is not null) Line("Backup: " + report.BackupPath);
        if (!string.IsNullOrEmpty(report.RestorePointWarning)) Line("Restore point: " + report.RestorePointWarning);
    }
}
