using System.Windows.Controls;

namespace Limpio.Views;

public partial class RestorePage : Page
{
    public RestorePage()
    {
        DataContext = App.Restore;
        InitializeComponent();
        Loaded += async (_, _) => await App.Restore.RefreshAsync();
    }
}
