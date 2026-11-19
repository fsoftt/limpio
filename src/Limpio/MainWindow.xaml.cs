using System.Windows;
using Limpio.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Limpio;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        ApplicationThemeManager.ApplySystemTheme();
        SystemThemeWatcher.Watch(this);
        Loaded += OnLoaded;
    }

    public static void NavigateTo(Type pageType) =>
        ((MainWindow)Application.Current.MainWindow).RootNavigation.Navigate(pageType);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var page = App.State.Options.Page?.ToLowerInvariant() switch
        {
            "tweaks" => typeof(TweaksPage),
            "apply" => typeof(ApplyPage),
            "results" => typeof(ResultsPage),
            "restore" => typeof(RestorePage),
            "about" => typeof(AboutPage),
            _ => typeof(HomePage),
        };
        RootNavigation.Navigate(page);
        if (App.State.Options.Shot is { } shot) _ = ShootAsync(shot);
    }

    /// <summary>Debug aid: renders the window content to a PNG and exits (used for README screenshots).</summary>
    private async Task ShootAsync(string path)
    {
        await Task.Delay(TimeSpan.FromSeconds(14));
        var visual = (FrameworkElement)Content;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var w = (int)(visual.ActualWidth * dpi.DpiScaleX);
        var h = (int)(visual.ActualHeight * dpi.DpiScaleY);
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
        bmp.Render(visual);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        using (var fs = System.IO.File.Create(path)) enc.Save(fs);
        Application.Current.Shutdown();
    }
}
