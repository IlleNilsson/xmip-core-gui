using Xmip.Surface;

namespace Xmip.Operations.Configuration;

/// <summary>
/// The cluster's <c>xmip.toml</c> the desktop edits — the one configuration
/// anyone edits (ADR-0031, amendment 2026-10-05: <i>node TOML files shall
/// not be edited, only cluster TOML files</i>). It holds the text and what
/// the runtime answers of it; it holds no configuration rule. Its views are
/// <c>xmip_cluster_views_v1</c>'s, every edit is made by
/// <c>xmip_cluster_edit_v1</c>, which keeps comments and layout and refuses
/// an edit that would leave a node unable to read its slice, and whether
/// the file is good is <c>xmip_validate_v1</c>'s, node by node.
/// </summary>
public sealed class ClusterConfiguration
{
    // Begun here: there was no file, or nothing in it, so it is a cluster's
    // file being written and may be edited before it declares a node.
    private readonly bool _begun;

    private ClusterConfiguration(string path, string text, bool exists)
    {
        Path = path;
        Text = text;
        Exists = exists;
        _begun = string.IsNullOrWhiteSpace(text);
        Views = new ConfigurationViews(string.Empty, false, []);
        Read();
    }

    /// <summary>The cluster's file.</summary>
    public string Path { get; }

    /// <summary>The text as the editor holds it, saved or not.</summary>
    public string Text { get; private set; }

    /// <summary>Whether the file is on disk.</summary>
    public bool Exists { get; private set; }

    /// <summary>Whether <see cref="Text"/> holds edits not saved yet.</summary>
    public bool Changed { get; private set; }

    /// <summary>The runtime's views of <see cref="Text"/>.</summary>
    public ConfigurationViews Views { get; private set; }

    /// <summary>Why the text cannot be edited, in the runtime's words: it is
    /// not TOML, or it is a node's own document. Empty when it can.</summary>
    public string Refusal { get; private set; } = string.Empty;

    /// <summary>Whether the text may be edited here.</summary>
    public bool Editable => Refusal.Length == 0;

    /// <summary>The cluster's name, as its <c>[service]</c> says; empty when
    /// it says none.</summary>
    public string Name => Views.Cluster;

    /// <summary>The nodes the file declares, as the runtime lists them.</summary>
    public IReadOnlyList<string> Nodes =>
        (string[])[.. Views.Of("node")?.Entries.Select(entry => entry.Name) ?? []];

    /// <summary>Open the cluster's file at <paramref name="path"/>. A file
    /// not there yet is a cluster begun here; it is written on save.</summary>
    /// <exception cref="IOException">The file is there and cannot be read.</exception>
    public static ClusterConfiguration Open(string path)
    {
        bool exists = File.Exists(path);
        string text = exists ? File.ReadAllText(path) : string.Empty;
        return new ClusterConfiguration(path, text, exists);
    }

    /// <summary>Make <paramref name="edit"/> through the runtime. False, with
    /// its refusal, when the runtime refuses it; the text is then as it
    /// was.</summary>
    public bool TryEdit(ClusterEdit edit, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(edit);

        if (!Editable)
        {
            refusal = Refusal;
            return false;
        }

        if (!edit.TryApply(Text, out string answer))
        {
            refusal = answer;
            return false;
        }

        Text = answer;
        Changed = true;
        Read();
        refusal = string.Empty;
        return true;
    }

    /// <summary>Save the text, replacing the file whole so a reader never
    /// sees half of it.</summary>
    public void Write()
    {
        string? directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temp = Path + ".writing";
        File.WriteAllText(temp, Text);
        File.Move(temp, Path, overwrite: true);
        Exists = true;
        Changed = false;
    }

    // What the runtime says of the text, and whether it may be edited: a
    // node's own document is not, in the slicing's own words.
    private void Read()
    {
        if (!ConfigurationViews.TryRead(Text, out ConfigurationViews views, out string refusal))
        {
            Views = views;
            Refusal = refusal;
            return;
        }

        Views = views;
        Refusal = string.Empty;

        if (!views.IsCluster && !_begun && !NodeSlice.TrySlice(Text, null, out _, out string why))
        {
            Refusal = why;
        }
    }
}
