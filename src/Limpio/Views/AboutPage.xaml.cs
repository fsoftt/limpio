using System.Reflection;
using System.Windows.Controls;

namespace Limpio.Views;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
        VersionText.Text = v.Split('+')[0];
    }
}
