namespace Xmip.Operations.Configuration;

/// <summary>
/// Where the node configurations live: every TOML document in one directory,
/// the whole cluster as a file per node (ADR-0031); configuration is the
/// desktop's (ADR-0014). Which of them is the node this desktop starts is not
/// read from a file's name: it is the one <c>xmip.gui.toml</c> declares as
/// <c>NodeConfiguration</c>, the document the desktop starts at launch
/// (capability and configuration decide, never a name). Nothing is written
/// here unasked: an empty directory is an empty list.
/// </summary>
/// <param name="directory">The directory the desktop keeps its node
/// configurations in.</param>
/// <param name="started">The configuration the desktop starts, as
/// <c>xmip.gui.toml</c> declares it, resolved; null where it starts
/// none.</param>
public sealed class ConfigStore(string directory, string? started)
{
    /// <summary>The directory the configurations are listed from.</summary>
    public string Directory()
    {
        return directory;
    }

    /// <summary>One configuration in the directory: its node name (from the
    /// document), its path, and whether it is the one the desktop
    /// starts.</summary>
    public sealed record Entry(string Name, string Path, bool Started);

    /// <summary>Every node configuration in the directory, the one the desktop
    /// starts first, then by name.</summary>
    public IReadOnlyList<Entry> List()
    {
        System.IO.Directory.CreateDirectory(directory);

        List<Entry> entries = [];
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "*.toml"))
        {
            string file = Path.GetFileNameWithoutExtension(path);

            // A file that is not TOML is still listed, by its file name, so the
            // operator can open it and read why rather than lose the list.
            string node;
            try
            {
                node = NodeConfiguration.Read(path).NodeName;
            }
            catch (FormatException)
            {
                node = "";
            }

            string name = string.IsNullOrWhiteSpace(node) ? file : node;
            entries.Add(new Entry(name, path, Starts(path)));
        }

        return (Entry[])
        [
            .. entries
                .OrderByDescending(entry => entry.Started)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal),
        ];
    }

    // Whether this is the document the desktop was told to start: the same
    // file, however the two paths were written.
    private bool Starts(string path)
    {
        return started is not null
            && string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(started),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
    }
}
