using Xmip.Operations.Configuration;

namespace Xmip.Operations.Test;

/// <summary>
/// Which configuration the desktop starts is declared, never read from a
/// file's name, and nothing is written into the directory unasked. Until
/// 2026-09-27 a file called <c>main</c> was the main one, and an empty
/// directory was seeded with three documents naming fixed Windows paths.
/// </summary>
public sealed class ConfigStoreTest : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-config-store-").FullName;

    [Fact]
    public void TheDeclaredConfigurationIsTheStartedOneWhateverItIsCalled()
    {
        string main = Write("main.xmip.toml", "alpha");
        string declared = Write("edge.xmip.toml", "beta");

        IReadOnlyList<ConfigStore.Entry> entries = new ConfigStore(_directory, declared).List();

        Assert.Equal(["beta", "alpha"], entries.Select(entry => entry.Name));
        Assert.True(entries[0].Started);
        Assert.False(entries.Single(entry => entry.Path == main).Started);
    }

    [Fact]
    public void NothingDeclaredIsNothingStartedAndAnEmptyDirectoryStaysEmpty()
    {
        Assert.Empty(new ConfigStore(_directory, null).List());
        Assert.Empty(Directory.EnumerateFiles(_directory));

        Write("main.xmip.toml", "alpha");

        Assert.False(new ConfigStore(_directory, null).List().Single().Started);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string Write(string file, string node)
    {
        string path = Path.Combine(_directory, file);
        File.WriteAllText(path, $"[service]\nnode_name = \"{node}\"\n");

        return path;
    }
}
