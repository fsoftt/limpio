using Limpio.Core.Catalog;

namespace Limpio.Core.Tests;

public class DocsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Limpio.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }

    [Fact]
    public void Tweaks_doc_is_up_to_date()
    {
        var path = Path.Combine(RepoRoot(), "docs", "tweaks.md");
        var expected = TweakDocs.ToMarkdown(TweakCatalog.LoadEmbedded()).Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("LIMPIO_UPDATE_DOCS") == "1")
            File.WriteAllText(path, expected);
        var actual = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";
        Assert.True(expected == actual, "docs/tweaks.md is outdated. Run: LIMPIO_UPDATE_DOCS=1 dotnet test");
    }
}
