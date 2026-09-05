using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Tomlyn.Extensions.Configuration;

namespace Xmip.Gui.Desktop.Configuration;

/// <summary>
/// A node's configuration — the cluster and node it belongs to, the modules it
/// loads, the Processes it runs, and the Receive and Send Locations (the
/// protocols) it works over. The shape is what <c>xmip-core-configure</c> parses
/// today (open problem 14 has not decided the format); this reads and writes it
/// so the desktop can configure a node. ADR-0014: configuration is the desktop's,
/// not the web's. TOML on disk (ADR-0031).
/// </summary>
public sealed class NodeConfiguration
{
    public string ServiceName { get; set; } = "";
    public string ClusterName { get; set; } = "";
    public string NodeName { get; set; } = "";

    /// <summary>Modules and Processes are read and preserved verbatim on save so
    /// editing Locations never drops them; the desktop edits Locations and
    /// identity first.</summary>
    public string PreservedModulesAndProcesses { get; set; } = "";

    public List<Location> ReceiveLocations { get; init; } = [];
    public List<Location> SendLocations { get; init; } = [];

    /// <summary>One Receive or Send Location: a named endpoint over a transport,
    /// addressed in that transport's own terms.</summary>
    public sealed class Location
    {
        public string Name { get; set; } = "";
        public bool Start { get; set; } = true;
        public string Transport { get; set; } = "file";
        public string Address { get; set; } = "";
    }

    /// <summary>Read a node configuration from a TOML file. A missing file is an
    /// empty configuration rather than an error, so the editor can start one.</summary>
    public static NodeConfiguration Read(string path)
    {
        NodeConfiguration config = new();
        if (!File.Exists(path))
        {
            return config;
        }

        IConfigurationRoot toml = new ConfigurationBuilder()
            .AddTomlFile(path, optional: true, reloadOnChange: false)
            .Build();

        config.ServiceName = toml["service:name"] ?? "";
        config.ClusterName = toml["service:cluster_name"] ?? "";
        config.NodeName = toml["service:node_name"] ?? "";

        ReadLocations(toml, "receive_locations", config.ReceiveLocations);
        ReadLocations(toml, "send_locations", config.SendLocations);

        config.PreservedModulesAndProcesses = PreserveBlocks(File.ReadAllText(path));

        return config;
    }

    /// <summary>Write the configuration back as TOML: the service, the preserved
    /// modules and Processes, then the Locations. Deterministic, so a round trip
    /// changes only what the operator changed.</summary>
    public void Write(string path)
    {
        StringBuilder toml = new();

        toml.AppendLine("[service]");
        AppendString(toml, "name", ServiceName);
        AppendString(toml, "cluster_name", ClusterName);
        AppendString(toml, "node_name", NodeName);
        toml.AppendLine();

        if (!string.IsNullOrWhiteSpace(PreservedModulesAndProcesses))
        {
            toml.AppendLine(PreservedModulesAndProcesses.TrimEnd());
            toml.AppendLine();
        }

        AppendLocations(toml, "receive_locations", ReceiveLocations);
        AppendLocations(toml, "send_locations", SendLocations);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temp = path + ".writing";
        File.WriteAllText(temp, toml.ToString());
        File.Move(temp, path, overwrite: true);
    }

    private static void ReadLocations(IConfigurationRoot toml, string key, List<Location> into)
    {
        foreach (IConfigurationSection row in toml.GetSection(key).GetChildren())
        {
            into.Add(new Location
            {
                Name = row["name"] ?? "",
                Start = !string.Equals(row["start"], "false", StringComparison.OrdinalIgnoreCase),
                Transport = row["transport"] ?? "file",
                Address = row["address"] ?? "",
            });
        }
    }

    private static void AppendLocations(StringBuilder toml, string key, List<Location> locations)
    {
        foreach (Location location in locations)
        {
            toml.AppendLine($"[[{key}]]");
            AppendString(toml, "name", location.Name);
            toml.AppendLine($"start = {(location.Start ? "true" : "false")}");
            AppendString(toml, "transport", location.Transport);
            AppendString(toml, "address", location.Address);
            toml.AppendLine();
        }
    }

    private static void AppendString(StringBuilder toml, string key, string value)
    {
        string escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        toml.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{key} = \"{escaped}\""));
    }

    /// <summary>The modules and Processes tables, verbatim, so editing service
    /// and Locations never drops them. A table header flips capture on when it is
    /// a modules or xmip_processes table and off at any other table.</summary>
    private static string PreserveBlocks(string original)
    {
        StringBuilder kept = new();
        bool capturing = false;

        foreach (string raw in original.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith('['))
            {
                capturing = trimmed.StartsWith("[[modules]]", StringComparison.Ordinal)
                    || trimmed.StartsWith("[modules.", StringComparison.Ordinal)
                    || trimmed.StartsWith("[[modules.", StringComparison.Ordinal)
                    || trimmed.StartsWith("[[xmip_processes]]", StringComparison.Ordinal)
                    || trimmed.StartsWith("[xmip_processes.", StringComparison.Ordinal);
            }

            if (capturing)
            {
                kept.AppendLine(line);
            }
        }

        return kept.ToString();
    }
}
