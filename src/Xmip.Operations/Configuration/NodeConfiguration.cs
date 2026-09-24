using Tomlyn.Syntax;
using Xmip.Surface;

namespace Xmip.Operations.Configuration;

/// <summary>
/// The desktop editor's view of a node's configuration document — the one
/// document <c>xmip-core-configure</c> reads (<c>XmipConfigurationDocument</c>).
/// ADR-0014: configuration is the desktop's, not the web's. TOML on disk
/// (ADR-0031).
/// </summary>
/// <remarks>
/// <para>This is an edit and display layer, not a model of its own. It holds
/// the document's syntax tree, parsed and edited through
/// <see cref="TomlDocument"/>, and each property reads or writes one key of
/// it, matched whole. An edit replaces one value in place; everything else —
/// comments, blank lines, the order of keys and tables, the modules and their
/// manifests, a Process's Subprocesses and Extensions, a key the editor does
/// not know — is written back exactly as it was read. Strings are unescaped
/// on read and escaped on write by the TOML library.</para>
/// <para>Nothing the runtime requires is supplied here. A Location or a
/// Process without <c>start</c> reads as <see langword="null"/> and is written
/// without it; a Location without <c>transport</c> likewise. A new Process
/// writes no <c>required_modules</c>, <c>xmip_subprocesses</c> or
/// <c>extensions</c>: the document reads them as empty (ADR-0031, amendment
/// 2026-09-24). Whether the document is good is the runtime's answer, through
/// <c>xmip_validate_v1</c> (ADR-0027, amendment 2026-09-05) — never this
/// class's. Until 2026-09-24 this class parsed the TOML by hand, matched keys
/// by prefix, never unescaped, and wrote <c>start = true</c> and
/// <c>transport = "file"</c> where the file had none (open problem 25, row
/// b).</para>
/// <para>The editor edits the document in the shape it is written in:
/// <c>[service]</c> as a table, Processes and Locations as <c>[[…]]</c>
/// rows. A document written another way — inline tables, dotted keys at the
/// top — is kept as it is and shown as far as that shape goes.</para>
/// </remarks>
public sealed class NodeConfiguration
{
    private static readonly string[] Service = ["service"];
    private static readonly string[] ProcessKey = ["xmip_processes"];
    private static readonly string[] ReceiveKey = ["receive_locations"];
    private static readonly string[] SendKey = ["send_locations"];

    private readonly DocumentSyntax _document;

    private NodeConfiguration(DocumentSyntax document)
    {
        _document = document;
        Processes = [.. Rows(ProcessKey).Select(row => new XmipProcess(row))];
        ReceiveLocations = [.. Rows(ReceiveKey).Select(row => new Location(row))];
        SendLocations = [.. Rows(SendKey).Select(row => new Location(row))];
    }

    /// <summary>An empty document, for a node not configured yet.</summary>
    public NodeConfiguration()
        : this(TomlDocument.Parse(""))
    {
    }

    /// <summary><c>[service] name</c>.</summary>
    public string ServiceName
    {
        get => ServiceText("name");
        set => TomlDocument.Set(ServiceTable(), "name", value);
    }

    /// <summary><c>[service] cluster_name</c>.</summary>
    public string ClusterName
    {
        get => ServiceText("cluster_name");
        set => TomlDocument.Set(ServiceTable(), "cluster_name", value);
    }

    /// <summary><c>[service] node_name</c>.</summary>
    public string NodeName
    {
        get => ServiceText("node_name");
        set => TomlDocument.Set(ServiceTable(), "node_name", value);
    }

    /// <summary>Whether the node may assume the internet (ADR-0045). Off unless
    /// said, which is the document's own rule, not the editor's.</summary>
    public bool Online
    {
        get => ExistingService() is { } service && TomlDocument.Flag(service, "online") is true;
        set => TomlDocument.Set(ServiceTable(), "online", value);
    }

    /// <summary>The Xmip Processes, in document order.</summary>
    public List<XmipProcess> Processes { get; }

    /// <summary>The Receive Locations, in document order.</summary>
    public List<Location> ReceiveLocations { get; }

    /// <summary>The Send Locations, in document order.</summary>
    public List<Location> SendLocations { get; }

    /// <summary>The execution styles, <c>xmip-core-configure</c>'s kebab-case
    /// values, for the editor to offer. A value outside them is kept as the
    /// file says and refused by the runtime, not replaced.</summary>
    public static readonly string[] ExecutionStyles = ["sequential", "parallel", "concurrent"];

    /// <summary>One Xmip Process: the keys the editor shows, over its row of the
    /// document. Its Subprocesses and Extensions stay in the document.</summary>
    public sealed class XmipProcess
    {
        internal XmipProcess(TableArraySyntax row)
        {
            Row = row;
        }

        /// <summary>A Process that is in no document yet, with no key set.</summary>
        public XmipProcess()
            : this(new TableArraySyntax())
        {
        }

        internal TableArraySyntax Row { get; }

        /// <summary><c>name</c>; empty when the row has none.</summary>
        public string Name
        {
            get => TomlDocument.Text(Row, "name") ?? "";
            set => TomlDocument.Set(Row, "name", value);
        }

        /// <summary><c>start</c>; <see langword="null"/> when the row has none.</summary>
        public bool? Start
        {
            get => TomlDocument.Flag(Row, "start");
            set => TomlDocument.Set(Row, "start", value);
        }

        /// <summary><c>execution_style</c>, as written; <see langword="null"/>
        /// when the row has none, which the document reads as sequential.</summary>
        public string? ExecutionStyle
        {
            get => TomlDocument.Text(Row, "execution_style");
            set => TomlDocument.Set(Row, "execution_style", string.IsNullOrEmpty(value) ? null : value);
        }
    }

    /// <summary>One Receive or Send Location over its row of the document: a
    /// named endpoint over a transport, addressed in that transport's own
    /// terms.</summary>
    public sealed class Location
    {
        internal Location(TableArraySyntax row)
        {
            Row = row;
        }

        /// <summary>A Location that is in no document yet, with no key set:
        /// the operator chooses its transport and whether it starts.</summary>
        public Location()
            : this(new TableArraySyntax())
        {
        }

        internal TableArraySyntax Row { get; }

        /// <summary><c>name</c>; empty when the row has none.</summary>
        public string Name
        {
            get => TomlDocument.Text(Row, "name") ?? "";
            set => TomlDocument.Set(Row, "name", value);
        }

        /// <summary><c>start</c>; <see langword="null"/> when the row has none.</summary>
        public bool? Start
        {
            get => TomlDocument.Flag(Row, "start");
            set => TomlDocument.Set(Row, "start", value);
        }

        /// <summary><c>transport</c>; <see langword="null"/> when the row has none.</summary>
        public string? Transport
        {
            get => TomlDocument.Text(Row, "transport");
            set => TomlDocument.Set(Row, "transport", string.IsNullOrEmpty(value) ? null : value);
        }

        /// <summary><c>address</c>; empty when the row has none.</summary>
        public string Address
        {
            get => TomlDocument.Text(Row, "address") ?? "";
            set => TomlDocument.Set(Row, "address", value);
        }
    }

    /// <summary>Read a node configuration from a TOML file. A missing file is an
    /// empty document rather than an error, so the editor can start one.</summary>
    /// <exception cref="FormatException">The file is not TOML.</exception>
    public static NodeConfiguration Read(string path)
    {
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : new NodeConfiguration();
    }

    /// <summary>The editor's view over the document <paramref name="text"/> is.</summary>
    /// <exception cref="FormatException">The text is not TOML.</exception>
    public static NodeConfiguration Parse(string text)
    {
        return new NodeConfiguration(TomlDocument.Parse(text));
    }

    /// <summary>The document as TOML text: what <see cref="Write"/> saves and
    /// what the runtime is asked to validate. A row the editor added joins the
    /// rows of its kind; a row it removed leaves with its own tables.</summary>
    public string ToToml()
    {
        Store(ProcessKey, [.. Processes.Select(process => process.Row)]);
        Store(ReceiveKey, [.. ReceiveLocations.Select(location => location.Row)]);
        Store(SendKey, [.. SendLocations.Select(location => location.Row)]);

        return _document.ToString();
    }

    /// <summary>Save the document, replacing the file whole so a reader never
    /// sees half of it.</summary>
    public void Write(string path)
    {
        string text = ToToml();

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temp = path + ".writing";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }

    private TableSyntax? ExistingService()
    {
        return TomlDocument.Tables<TableSyntax>(_document, Service).FirstOrDefault();
    }

    private string ServiceText(string key)
    {
        return ExistingService() is { } service ? TomlDocument.Text(service, key) ?? "" : "";
    }

    private TableSyntax ServiceTable()
    {
        if (ExistingService() is { } service)
        {
            return service;
        }

        TableSyntax created = new();
        TomlDocument.Insert(_document, created, Service);
        return created;
    }

    private List<TableArraySyntax> Rows(string[] key)
    {
        return [.. TomlDocument.Tables<TableArraySyntax>(_document, key)];
    }

    private void Store(string[] key, List<TableArraySyntax> wanted)
    {
        List<TableArraySyntax> present = Rows(key);

        foreach (TableArraySyntax gone in present.Except(wanted))
        {
            TomlDocument.Remove(_document, gone);
        }

        foreach (TableArraySyntax added in wanted.Except(present))
        {
            TomlDocument.Insert(_document, added, key);
        }
    }
}
