using System.Collections.Specialized;
using System.Windows.Controls;

namespace Limpio.Views;

public partial class ApplyPage : Page
{
    public ApplyPage()
    {
        DataContext = App.Apply;
        InitializeComponent();
        Loaded += (_, _) => App.Apply.Refresh();
        ((INotifyCollectionChanged)App.Apply.Log).CollectionChanged += (_, _) =>
        {
            if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[^1]);
        };
    }
}
