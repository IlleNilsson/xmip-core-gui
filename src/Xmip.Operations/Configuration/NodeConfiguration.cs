using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Tomlyn.Extensions.Configuration;

namespace Xmip.Operations.Configuration;

/// <summary>
/// A node's configuration — the cluster and node it belongs to, the modules it
/// loads, the Xmip Processes it runs (with their execution style), and the
/// Receive and Send Locations it works over. The shape is what
/// <c>xmip-core-configure</c> parses today (open problem 14 has not decided the
/// format); this reads and writes it so the desktop can configure a node.
/// ADR-0014: configuration is the desktop's, not the web's. TOML on disk
/// (ADR-0031).
/// </summary>
public sealed class NodeConfiguration
{
    public string ServiceName { get; set; } = "";
    public string ClusterName { get; set; } = "";
    public string NodeName { get; set; } = "";

    /// <summary>Whether the node may assume the internet (ADR-0045). Off unless said.</summary>
    public bool Online { get; set; }

    /// <summary>Modules are read and preserved verbatim on save so editing never
    /// drops them; their manifests are richer than the editor models.</summary>
    public string PreservedModules { get; set; } = "";

    public List<XmipProcess> Processes { get; init; } = [];
    public List<Location> ReceiveLocations { get; init; } = [];
    public List<Location> SendLocations { get; init; } = [];

    /// <summary>One Xmip Process: a name, whether it starts, and how its work runs
    /// (the execution style — the throughput lever). Any deeper configuration of
    /// the Process (required modules, Subprocesses, Extensions) is preserved
    /// verbatim in <see cref="Body"/> so editing the style never drops it.</summary>
    public sealed class XmipProcess
    {
        public string Name { get; set; } = "";
        public bool Start { get; set; } = true;
        public string ExecutionStyle { get; set; } = "sequential";
        public string Body { get; set; } = "";
    }

    /// <summary>The execution styles, matching <c>xmip-core-configure</c>'s
    /// kebab-case values. Sequential is the safe default; Parallel and Concurrent
    /// trade ordering for throughput.</summary>
    public static readonly string[] ExecutionStyles = ["sequential", "parallel", "concurrent"];

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
        config.Online = string.Equals(toml["service:online"], "true", StringComparison.OrdinalIgnoreCase);

        ReadLocations(toml, "receive_locations", config.ReceiveLocations);
        ReadLocations(toml, "send_locations", config.SendLocations);

        config.ParseBlocks(File.ReadAllText(path));

        return config;
    }

    /// <summary>Write the configuration back as TOML: the service, the preserved
    /// modules, the Processes with their execution style, then the Locations.
    /// Deterministic, so a round trip changes only what the operator changed.</summary>
    public void Write(string path)
    {
        StringBuilder toml = new();

        toml.AppendLine("[service]");
        AppendString(toml, "name", ServiceName);
        AppendString(toml, "cluster_name", ClusterName);
        AppendString(toml, "node_name", NodeName);
        // ADR-0045: offline unless the operator says otherwise.
        toml.AppendLine($"online = {(Online ? "true" : "false")}");
        toml.AppendLine();

        if (!string.IsNullOrWhiteSpace(PreservedModules))
        {
            toml.AppendLine(PreservedModules.TrimEnd());
            toml.AppendLine();
        }

        AppendProcesses(toml);
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

    private void AppendProcesses(StringBuilder toml)
    {
        foreach (XmipProcess process in Processes)
        {
            toml.AppendLine("[[xmip_processes]]");
            AppendString(toml, "name", process.Name);
            toml.AppendLine($"start = {(process.Start ? "true" : "false")}");
            AppendString(toml, "execution_style", NormaliseStyle(process.ExecutionStyle));
            if (!string.IsNullOrWhiteSpace(process.Body))
            {
                toml.AppendLine(process.Body.TrimEnd('\n', '\r'));
            }
            toml.AppendLine();
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

    private static string NormaliseStyle(string style)
    {
        string lower = style.Trim().ToLowerInvariant();
        return Array.IndexOf(ExecutionStyles, lower) >= 0 ? lower : "sequential";
    }

    /// <summary>Split the document into the modules kept verbatim and the Xmip
    /// Processes modelled as editable items. A <c>[[modules]]</c> family header
    /// captures into the modules text; a top-level <c>[[xmip_processes]]</c>
    /// starts a Process, whose own name / start / execution_style are modelled and
    /// whose deeper tables (Subprocesses, Extensions) are kept in its Body; any
    /// other header (service, locations — modelled elsewhere) captures nothing.</summary>
    private void ParseBlocks(string original)
    {
        StringBuilder modules = new();
        XmipProcess? current = null;
        bool ownFields = false;
        Capture capture = Capture.None;

        foreach (string raw in original.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith('['))
            {
                if (trimmed.StartsWith("[[xmip_processes]]", StringComparison.Ordinal))
                {
                    current = new XmipProcess();
                    Processes.Add(current);
                    ownFields = true;
                    capture = Capture.Process;
                    continue;
                }

                if (trimmed.StartsWith("[xmip_processes.", StringComparison.Ordinal)
                    || trimmed.StartsWith("[[xmip_processes.", StringComparison.Ordinal))
                {
                    ownFields = false;
                    current?.AppendBody(line);
                    capture = Capture.Process;
                    continue;
                }

                capture = ModuleHeader(trimmed) ? Capture.Modules : Capture.None;
                if (capture == Capture.Modules)
                {
                    modules.AppendLine(line);
                }
                continue;
            }

            switch (capture)
            {
                case Capture.Modules:
                    modules.AppendLine(line);
                    break;
                case Capture.Process when current is not null:
                    CaptureProcessLine(current, ownFields, line, trimmed);
                    break;
                case Capture.None:
                default:
                    break;
            }
        }

        PreservedModules = modules.ToString();
    }

    private static void CaptureProcessLine(XmipProcess process, bool ownFields, string line, string trimmed)
    {
        if (ownFields && trimmed.StartsWith("name", StringComparison.Ordinal))
        {
            process.Name = Unquote(trimmed);
        }
        else if (ownFields && trimmed.StartsWith("start", StringComparison.Ordinal))
        {
            process.Start = trimmed.Contains("true", StringComparison.OrdinalIgnoreCase);
        }
        else if (ownFields && trimmed.StartsWith("execution_style", StringComparison.Ordinal))
        {
            process.ExecutionStyle = NormaliseStyle(Unquote(trimmed));
        }
        else
        {
            process.AppendBody(line);
        }
    }

    private static bool ModuleHeader(string trimmed)
    {
        return trimmed.StartsWith("[[modules]]", StringComparison.Ordinal)
            || trimmed.StartsWith("[modules.", StringComparison.Ordinal)
            || trimmed.StartsWith("[[modules.", StringComparison.Ordinal);
    }

    private static string Unquote(string line)
    {
        int open = line.IndexOf('"', StringComparison.Ordinal);
        int close = line.LastIndexOf('"');
        return open >= 0 && close > open ? line.Substring(open + 1, close - open - 1) : "";
    }

    private enum Capture
    {
        None,
        Modules,
        Process,
    }
}

internal static class XmipProcessExtensions
{
    internal static void AppendBody(this NodeConfiguration.XmipProcess process, string line)
    {
        process.Body = string.IsNullOrEmpty(process.Body) ? line : process.Body + "\n" + line;
    }
}
