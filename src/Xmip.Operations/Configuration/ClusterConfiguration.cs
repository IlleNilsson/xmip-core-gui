using System.Security.Cryptography;
using System.Text;
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

    // The file as this editor last read or wrote it, as its SHA-256; empty
    // where there was no file. A save compares the disk against it, so an
    // edit made elsewhere meanwhile is never overwritten unseen — the VS Code
    // designer refuses an edit on the same ground, by the document's version.
    private string _read;

    private ClusterConfiguration(string path, string text, bool exists, string read)
    {
        Path = path;
        Text = text;
        Exists = exists;
        _read = read;
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
        if (!File.Exists(path))
        {
            return new ClusterConfiguration(path, string.Empty, false, string.Empty);
        }

        // Read once: the text and its fingerprint are of the same bytes.
        byte[] bytes = File.ReadAllBytes(path);
        using StreamReader reader = new(new MemoryStream(bytes), Encoding.UTF8, true);
        return new ClusterConfiguration(path, reader.ReadToEnd(), true, Fingerprint(bytes));
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

    /// <summary>
    /// Save the text, replacing the file whole so a reader never sees half
    /// of it: written to a temporary file of its own beside it, flushed to the
    /// device, then renamed over it. False, with why, when the file changed
    /// on disk since this editor read it — another editor saved meanwhile —
    /// or another save holds the file now, and nothing is written;
    /// <see cref="Open"/> it again to see that change.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read or written.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be written.</exception>
    public bool TryWrite(out string refusal)
    {
        string? directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // The check and the rename are one step for every Xmip writer: each
        // holds <file>.lock, opened exclusively, from before the comparison
        // until after the rename, so two saves never both pass the check. A
        // save that finds the lock held is refused, not queued: the other
        // save changes the file, and this editor has to see that change.
        using FileStream? held = Hold();
        if (held is null)
        {
            refusal = $"another save of {System.IO.Path.GetFileName(Path)} is under way; "
                + "nothing was saved. Reload it to see the other change, then edit again.";
            return false;
        }

        string now = File.Exists(Path) ? Fingerprint(File.ReadAllBytes(Path)) : string.Empty;
        if (!string.Equals(now, _read, StringComparison.Ordinal))
        {
            refusal = $"{System.IO.Path.GetFileName(Path)} changed on disk since it was opened "
                + "here; nothing was saved. Reload it to see the other change, then edit again.";
            return false;
        }

        byte[] bytes = new UTF8Encoding(false).GetBytes(Text);
        string temp = $"{Path}.{Guid.NewGuid():n}.writing";

        try
        {
            using (FileStream file = new(temp, FileMode.CreateNew, FileAccess.Write))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }

            File.Move(temp, Path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }

        _read = Fingerprint(bytes);
        Exists = true;
        Changed = false;
        refusal = string.Empty;
        return true;
    }

    // The save lock: <file>.lock, shared with nobody and deleted when it is
    // closed. Null when another save holds it; any other failure to open it
    // is the caller's, as for the file itself.
    private FileStream? Hold()
    {
        try
        {
            return new FileStream(
                $"{Path}.lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException) when (File.Exists($"{Path}.lock"))
        {
            return null;
        }
    }

    private static string Fingerprint(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
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
