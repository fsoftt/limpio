namespace Limpio.Core;

/// <summary>Pure decisions behind the mouse wheel forwarding of the app (kept here so they can be tested).</summary>
public static class ScrollLogic
{
    /// <summary>
    /// True when a viewer can still move for this wheel delta (positive = up). A viewer with a disabled vertical
    /// scrollbar or nothing to scroll cannot.
    /// </summary>
    public static bool CanScrollVertically(bool verticalDisabled, double scrollableHeight, double verticalOffset, int delta)
    {
        if (verticalDisabled || scrollableHeight <= 0 || delta == 0) return false;
        return delta > 0 ? verticalOffset > 0 : verticalOffset < scrollableHeight;
    }

    /// <summary>New vertical offset after a wheel delta, clamped to the scrollable range.</summary>
    public static double NextOffset(double verticalOffset, double scrollableHeight, int delta) =>
        Math.Clamp(verticalOffset - delta, 0, Math.Max(0, scrollableHeight));
}
