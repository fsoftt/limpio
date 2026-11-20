using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Limpio.Core;

namespace Limpio.Services;

/// <summary>
/// A ScrollViewer marks the mouse wheel as handled even when it has nothing to scroll (or is already at its end),
/// so the wheel and the touchpad two-finger scroll stop working over lists nested in a scrolling page.
/// This class handler scrolls the nearest ancestor ScrollViewer that can still move in those cases.
/// Registered once at startup.
/// </summary>
public static class WheelForwarding
{
    public static void Register() =>
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel), handledEventsToo: false);

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not ScrollViewer sv) return;
        if (CanScroll(sv, e.Delta)) return; // inner viewer still scrolls: leave it alone
        if (InOpenDropDown(sv)) return;

        for (var parent = ParentOf(sv); parent is not null; parent = ParentOf(parent))
        {
            if (parent is not ScrollViewer target || !CanScroll(target, e.Delta)) continue;
            target.ScrollToVerticalOffset(ScrollLogic.NextOffset(target.VerticalOffset, target.ScrollableHeight, e.Delta));
            e.Handled = true; // something actually scrolled
            return;
        }
    }

    private static bool CanScroll(ScrollViewer sv, int delta) =>
        ScrollLogic.CanScrollVertically(sv.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled,
            sv.ScrollableHeight, sv.VerticalOffset, delta);

    private static bool InOpenDropDown(DependencyObject start)
    {
        for (var d = ParentOf(start); d is not null; d = ParentOf(d))
            if (d is ComboBox { IsDropDownOpen: true }) return true;
        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject d) =>
        d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
}
