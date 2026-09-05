using Microsoft.Extensions.Configuration;

namespace Xmip.Gui.Desktop.Configuration;

/// <summary>
/// Where the node configurations live, and what is there by default. The main
/// configuration is sliced into its nodes — a file per node — so "the whole
/// cluster" is the set of node configs in one directory. On first run the
/// directory is seeded with the main node and a couple of edge nodes, so the
/// editor opens on the main and all nodes present rather than an empty path.
/// TOML on disk (ADR-0031); configuration is the desktop's (ADR-0014).
/// </summary>
public sealed class ConfigStore(IConfiguration configuration)
{
    /// <summary>The configured directory, or a writable default under app data.</summary>
    public string Directory()
    {
        string? configured = configuration["Xmip:ConfigDirectory"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "xmip", "config")
            : configured;
    }

    /// <summary>One configuration in the directory: its node name (from the file)
    /// and its path.</summary>
    public sealed record Entry(string Name, string Path, bool IsMain);

    /// <summary>Every node configuration in the directory, the main first, seeding
    /// the directory on first run so the main and all nodes are present.</summary>
    public IReadOnlyList<Entry> List()
    {
        string directory = Directory();
        System.IO.Directory.CreateDirectory(directory);
        Seed(directory);

        List<Entry> entries = [];
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "*.toml"))
        {
            string file = Path.GetFileNameWithoutExtension(path);
            bool isMain = file.Equals("main", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".main", StringComparison.OrdinalIgnoreCase);
            NodeConfiguration node = NodeConfiguration.Read(path);
            string name = string.IsNullOrWhiteSpace(node.NodeName) ? file : node.NodeName;
            entries.Add(new Entry(name, path, isMain));
        }

        return
        [
            .. entries
                .OrderByDescending(entry => entry.IsMain)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal),
        ];
    }

    private static void Seed(string directory)
    {
        if (System.IO.Directory.EnumerateFiles(directory, "*.toml").Any())
        {
            return;
        }

        Write(directory, "main.xmip.toml", "main", "http", "http://0.0.0.0:8080/orders",
            "file", "C:/xmip/main/out/billing");
        Write(directory, "edge-01.xmip.toml", "edge-01", "file", "C:/xmip/edge-01/in/orders",
            "smtp", "mailto:billing@example.com");
        Write(directory, "edge-02.xmip.toml", "edge-02", "tcp", "0.0.0.0:9001",
            "tcp", "warehouse.example.com:9100");
    }

    private static void Write(
        string directory, string file, string node,
        string receiveTransport, string receiveAddress,
        string sendTransport, string sendAddress)
    {
        NodeConfiguration config = new()
        {
            ServiceName = $"xmip-{node}",
            ClusterName = "lab",
            NodeName = node,
        };
        config.ReceiveLocations.Add(new NodeConfiguration.Location
        {
            Name = "orders-in",
            Transport = receiveTransport,
            Address = receiveAddress,
            Start = true,
        });
        config.SendLocations.Add(new NodeConfiguration.Location
        {
            Name = "billing-out",
            Transport = sendTransport,
            Address = sendAddress,
            Start = true,
        });
        config.Write(Path.Combine(directory, file));
    }
}
