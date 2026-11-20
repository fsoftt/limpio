using System.Collections.Specialized;
using System.Windows.Controls;

namespace Limpio.Views;

public partial class ApplyPage : Page
{
    private void OnCopyLine(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Limpio.ViewModels.LogLine line }) App.Apply.CopyLineCommand.Execute(line.Text);
    }

    public ApplyPage()
    {
        DataContext = App.Apply;
        InitializeComponent();
        Loaded += async (_, _) => { App.Apply.Refresh(); await App.Apply.ProbeProtectionAsync(); };
        ((INotifyCollectionChanged)App.Apply.Log).CollectionChanged += (_, _) =>
        {
            LogScroll.ScrollToEnd();
        };
    }
}
