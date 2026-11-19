using Limpio.Core.Platform;

namespace Limpio.Core.Models;

/// <summary>Aggregated scan result of a tweak.</summary>
public sealed record TweakState(Tweak Tweak, ActionState State, IReadOnlyList<ActionState> ActionStates);

public static class Scanner
{
    public static async Task<IReadOnlyList<TweakState>> ScanAsync(
        IEnumerable<Tweak> tweaks, SystemContext ctx, CancellationToken ct = default)
    {
        var result = new List<TweakState>();
        foreach (var t in tweaks)
        {
            ct.ThrowIfCancellationRequested();
            var states = new List<ActionState>();
            foreach (var a in t.Actions)
            {
                try { states.Add(await a.DetectAsync(ctx)); }
                catch { states.Add(ActionState.NotApplied); }
            }
            result.Add(new TweakState(t, Aggregate(states), states));
        }
        return result;
    }

    public static ActionState Aggregate(IReadOnlyList<ActionState> states)
    {
        if (states.Count == 0 || states.All(s => s == ActionState.NotPresent)) return ActionState.NotPresent;
        if (states.All(s => s != ActionState.NotApplied)) return ActionState.Applied;
        return ActionState.NotApplied;
    }
}
