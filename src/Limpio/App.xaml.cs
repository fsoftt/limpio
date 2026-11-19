using System.Windows;
using Limpio.Services;
using Limpio.ViewModels;

namespace Limpio;

public partial class App : Application
{
    public static AppState State { get; private set; } = null!;
    public static TweaksViewModel Tweaks { get; private set; } = null!;
    public static ApplyViewModel Apply { get; private set; } = null!;
    public static ResultsViewModel Results { get; private set; } = null!;
    public static RestoreViewModel Restore { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WheelForwarding.Register();
        var options = StartupOptions.Parse(e.Args);
        Loc.Init(options.Lang);
        State = new AppState(options);
        Tweaks = new TweaksViewModel(State);
        Apply = new ApplyViewModel(State, Tweaks);
        Results = new ResultsViewModel(State);
        Restore = new RestoreViewModel(State);

        var window = new MainWindow();
        window.Show();
        _ = StartAsync();
    }

    private static async Task StartAsync()
    {
        await State.LoadSavedSnapshotsAsync();
        await State.ScanAsync();
    }
}
