using Tomlyn.Syntax;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// The Desktop's store of the bar's order: a local TOML file of the
/// operator's own, <c>preferences.toml</c> in the app's data folder, its
/// <c>[bar]</c> table's <c>order</c> holding what
/// <see cref="BarOrder.Say"/> writes (the owner, 2026-11-12: "The Desktop
/// version can have a local TOML file, like for cookies"). The file is
/// edited in place, so anything else in it stays as written; a file that is
/// not TOML is read as the default and replaced when an order is kept.
/// </summary>
public sealed class BarOrderFile(string path) : IBarOrderStore
{
    /// <summary>The file's name in the app's data folder.</summary>
    public const string Name = "preferences.toml";

    private readonly Lock guard = new();

    // The order as last read or kept; null until it is read.
    private IReadOnlyList<BarTab>? Cached { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<BarTab> Order
    {
        get
        {
            lock (guard)
            {
                return Cached ??= BarOrder.Read(Kept(Document()));
            }
        }
    }

    /// <inheritdoc />
    public void Keep(IReadOnlyList<BarTab>? arranged)
    {
        lock (guard)
        {
            DocumentSyntax document = Document() ?? TomlDocument.Parse(string.Empty);
            TableSyntax? bar = TomlDocument.Tables<TableSyntax>(document, "bar").FirstOrDefault();

            if (bar is null && arranged is not null)
            {
                bar = new TableSyntax();
                TomlDocument.Insert(document, bar, "bar");
            }

            if (bar is not null)
            {
                TomlDocument.Set(bar, "order", arranged is null ? null : BarOrder.Say(arranged));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, document.ToString());
            Cached = arranged is null ? BarOrder.Default : BarOrder.Read(BarOrder.Say(arranged));
        }
    }

    // The file as TOML; null where it is absent or not TOML.
    private DocumentSyntax? Document()
    {
        try
        {
            return File.Exists(path) ? TomlDocument.Parse(File.ReadAllText(path)) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? Kept(DocumentSyntax? document)
    {
        return document is not null
            && TomlDocument.Tables<TableSyntax>(document, "bar").FirstOrDefault() is { } bar
            ? TomlDocument.Text(bar, "order")
            : null;
    }
}
