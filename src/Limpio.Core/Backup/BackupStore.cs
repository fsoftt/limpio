using System.Text.Json;
using System.Text.Json.Serialization;

namespace Limpio.Core.Backup;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public interface IBackupStore
{
    Task<string> SaveAsync(BackupFile backup);
    Task<IReadOnlyList<BackupFile>> ListAsync();
    Task DeleteAsync(BackupFile backup);
}

/// <summary>Stores JSON backups in a folder (default %LOCALAPPDATA%\Limpio\backups).</summary>
public sealed class BackupStore : IBackupStore
{
    private readonly string _folder;

    public BackupStore(string? folder = null) => _folder = folder ?? AppPaths.Backups;

    public static string Serialize(BackupFile file) => JsonSerializer.Serialize(file, Json.Options);

    public static BackupFile Deserialize(string json) =>
        JsonSerializer.Deserialize<BackupFile>(json, Json.Options) ?? new BackupFile();

    public async Task<string> SaveAsync(BackupFile backup)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, backup.CreatedUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss") + ".json");
        await File.WriteAllTextAsync(path, Serialize(backup));
        backup.FilePath = path;
        return path;
    }

    public async Task<IReadOnlyList<BackupFile>> ListAsync()
    {
        var list = new List<BackupFile>();
        if (!Directory.Exists(_folder)) return list;
        foreach (var f in Directory.GetFiles(_folder, "*.json").OrderByDescending(x => x))
        {
            try
            {
                var b = Deserialize(await File.ReadAllTextAsync(f));
                b.FilePath = f;
                list.Add(b);
            }
            catch (JsonException) { /* ignore corrupt file */ }
        }
        return list;
    }

    public Task DeleteAsync(BackupFile backup)
    {
        if (backup.FilePath is not null && File.Exists(backup.FilePath)) File.Delete(backup.FilePath);
        return Task.CompletedTask;
    }
}
