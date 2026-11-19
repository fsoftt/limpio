using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Limpio.Views;

public partial class ResultsPage : Page
{
    public ResultsPage()
    {
        DataContext = App.Results;
        InitializeComponent();
    }

    private void OnExportPng(object sender, RoutedEventArgs e)
    {
        if (!App.Results.HasData) return;
        var dlg = new SaveFileDialog { Filter = "PNG|*.png", FileName = "limpio-resultados.png" };
        if (dlg.ShowDialog() != true) return;

        ExportRoot.UpdateLayout();
        var w = (int)Math.Ceiling(ExportRoot.ActualWidth);
        var h = (int)Math.Ceiling(ExportRoot.ActualHeight);
        var dpi = VisualTreeHelper.GetDpi(this);
        var bmp = new RenderTargetBitmap((int)(w * dpi.DpiScaleX), (int)(h * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bmp.Render(ExportRoot);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(dlg.FileName);
        enc.Save(fs);
        App.Results.Status = Services.Loc.F("results.saved", dlg.FileName);
    }

    private void OnExportMd(object sender, RoutedEventArgs e)
    {
        if (!App.Results.HasData) return;
        var dlg = new SaveFileDialog { Filter = "Markdown|*.md", FileName = "limpio-resultados.md" };
        if (dlg.ShowDialog() == true) App.Results.SaveMarkdown(dlg.FileName);
    }
}
