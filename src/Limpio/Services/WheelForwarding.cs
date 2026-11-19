using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Limpio.Services;

/// <summary>
/// A ScrollViewer marks the mouse wheel as handled even when it has nothing to scroll (or is already at its end),
/// so the wheel and the touchpad two-finger scroll stop working over lists nested in a scrolling page.
/// This class handler hands the wheel to the nearest parent ScrollViewer in those cases. Registered once at startup.
/// </summary>
public static class WheelForwarding
{
    public static void Register() =>
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel), handledEventsToo: false);

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not ScrollViewer sv) return;
        if (CanScroll(sv, e.Delta)) return;
        var parent = FindParentScrollViewer(sv);
        if (parent is null) return;

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent,
            Source = sv,
        });
    }

    private static bool CanScroll(ScrollViewer sv, int delta)
    {
        if (sv.ScrollableHeight <= 0) return false;
        return delta > 0 ? sv.VerticalOffset > 0 : sv.VerticalOffset < sv.ScrollableHeight;
    }

    private static ScrollViewer? FindParentScrollViewer(DependencyObject start)
    {
        var d = VisualTreeHelper.GetParent(start);
        while (d is not null)
        {
            if (d is ScrollViewer s) return s;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }
}
