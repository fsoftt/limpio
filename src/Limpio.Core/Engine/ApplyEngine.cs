using Limpio.Core.Actions;
using Limpio.Core.Backup;
using Limpio.Core.Models;
using Limpio.Core.Platform;

namespace Limpio.Core.Engine;

public enum StepResult { Applied, Skipped, Protected, Failed, Reverted }

public sealed record ApplyEvent(string TweakId, string Target, StepResult Result, string Message, int Done, int Total);

public sealed class ApplyReport
{
    public List<ApplyEvent> Events { get; } = new();
    public string? BackupPath { get; set; }
    public string? RestorePointWarning { get; set; }
    public int Failed => Events.Count(e => e.Result == StepResult.Failed);
    public int Applied => Events.Count(e => e.Result is StepResult.Applied or StepResult.Reverted);
}

public sealed class ApplyEngine
{
    private readonly SystemContext _ctx;
    private readonly IBackupStore _backups;
    private readonly IRestorePointService _restore;

    public ApplyEngine(SystemContext ctx, IBackupStore backups, IRestorePointService restore)
    {
        _ctx = ctx; _backups = backups; _restore = restore;
    }

    /// <summary>
    /// Applies the tweaks. A failing action is logged and the run continues.
    /// The backup is written even when the run is cancelled or errors.
    /// </summary>
    public async Task<ApplyReport> ApplyAsync(
        IReadOnlyList<Tweak> tweaks, IProgress<ApplyEvent>? progress = null, CancellationToken ct = default)
    {
        var report = new ApplyReport();
        var backup = new BackupFile();
        var total = tweaks.Sum(t => t.Actions.Count);
        var done = 0;

        void Emit(string id, string target, StepResult r, string msg)
        {
            var ev = new ApplyEvent(id, target, r, msg, done, total);
            report.Events.Add(ev);
            progress?.Report(ev);
        }

        try
        {
            report.RestorePointWarning = await SafeRestorePoint();
            backup.RestorePointWarning = report.RestorePointWarning;

            foreach (var tweak in tweaks)
            {
                foreach (var action in tweak.Actions)
                {
                    ct.ThrowIfCancellationRequested();
                    done++;
                    if (action.IsProtected)
                    {
                        Emit(tweak.Id, action.Target, StepResult.Protected, "Protected target, not touched");
                        continue;
                    }
                    try
                    {
                        var state = await action.DetectAsync(_ctx);
                        if (state != ActionState.NotApplied)
                        {
                            Emit(tweak.Id, action.Target, StepResult.Skipped,
                                state == ActionState.Applied ? "Already applied" : "Not present");
                            continue;
                        }
                        var entry = await action.ApplyAsync(_ctx);
                        entry.TweakId = tweak.Id;
                        backup.Entries.Add(entry);
                        Emit(tweak.Id, action.Target, StepResult.Applied, "OK");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Emit(tweak.Id, action.Target, StepResult.Failed, ex.Message);
                    }
                }
            }
        }
        finally
        {
            if (backup.Entries.Count > 0)
                report.BackupPath = await _backups.SaveAsync(backup);
        }
        return report;
    }

    private async Task<string?> SafeRestorePoint()
    {
        try { return await _restore.CreateAsync("Limpio"); }
        catch (Exception ex) { return ex.Message; }
    }
}

public sealed class RevertEngine
{
    private readonly SystemContext _ctx;

    public RevertEngine(SystemContext ctx) => _ctx = ctx;

    /// <summary>Reverts registry, service and task entries in reverse order. Apps are reported, not restored.</summary>
    public async Task<ApplyReport> RevertAsync(
        BackupFile backup, IProgress<ApplyEvent>? progress = null, CancellationToken ct = default)
    {
        var report = new ApplyReport { BackupPath = backup.FilePath };
        var total = backup.Entries.Count;
        var done = 0;
        foreach (var e in Enumerable.Reverse(backup.Entries))
        {
            ct.ThrowIfCancellationRequested();
            done++;
            StepResult r; string msg;
            try
            {
                switch (e.ActionKind)
                {
                    case "registry":
                        await new RegistryAction().RevertAsync(_ctx, e); r = StepResult.Reverted; msg = "OK"; break;
                    case "service":
                        await new ServiceAction { Name = e.Target }.RevertAsync(_ctx, e); r = StepResult.Reverted; msg = "OK"; break;
                    case "scheduledTask":
                        await new ScheduledTaskAction { Path = e.Target }.RevertAsync(_ctx, e); r = StepResult.Reverted; msg = "OK"; break;
                    default:
                        r = StepResult.Skipped;
                        msg = e.RevertHint ?? string.Join(", ", e.RemovedPackages.Select(p => p.StoreLink));
                        break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                r = StepResult.Failed; msg = ex.Message;
            }
            var ev = new ApplyEvent(e.TweakId, e.Target, r, msg, done, total);
            report.Events.Add(ev);
            progress?.Report(ev);
        }
        return report;
    }
}
