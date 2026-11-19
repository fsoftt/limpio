using System.Windows;
using System.Windows.Controls;

namespace Limpio.Views;

public partial class HomePage : Page
{
    public HomePage()
    {
        DataContext = App.State;
        InitializeComponent();
    }

    private async void OnScan(object sender, RoutedEventArgs e) => await App.State.ScanAsync();

    private void OnReview(object sender, RoutedEventArgs e) => MainWindow.NavigateTo(typeof(TweaksPage));

    private void OnRestartAdmin(object sender, RoutedEventArgs e) => App.State.RestartAsAdministrator();
}
