using System.Windows;
using System.Windows.Controls;

namespace Limpio.Views;

public partial class TweaksPage : Page
{
    public TweaksPage()
    {
        DataContext = App.Tweaks;
        InitializeComponent();
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) => App.Tweaks.SelectAll(true);

    private void OnSelectNone(object sender, RoutedEventArgs e) => App.Tweaks.SelectAll(false);
}
