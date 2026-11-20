namespace Limpio.Core;

public static class AppPaths
{
    public static string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Limpio");
    public static string Backups => Path.Combine(Root, "backups");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Snapshots => Path.Combine(Root, "snapshots");
}
