namespace Limpio.Core.Models;

public sealed class LocalizedText
{
    public string Es { get; set; } = "";
    public string En { get; set; } = "";

    public string Get(string language) =>
        language.StartsWith("es", StringComparison.OrdinalIgnoreCase)
            ? (string.IsNullOrEmpty(Es) ? En : Es)
            : (string.IsNullOrEmpty(En) ? Es : En);
}
