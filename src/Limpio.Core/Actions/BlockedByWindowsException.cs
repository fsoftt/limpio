namespace Limpio.Core.Actions;

/// <summary>
/// Windows refused a write that is normally allowed for administrators (for example values guarded by the
/// User Choice Protection Driver, UCPD). The action is reported as "blocked", not as a generic failure.
/// </summary>
public sealed class BlockedByWindowsException : Exception
{
    /// <summary>True when the UCPD service was running when the write was refused.</summary>
    public bool UcpdRunning { get; }

    public BlockedByWindowsException(string message, bool ucpdRunning, Exception? inner = null)
        : base(message, inner) => UcpdRunning = ucpdRunning;
}
