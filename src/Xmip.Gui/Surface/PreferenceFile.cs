using Tomlyn.Syntax;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// The Desktop's store of the operator's view: a local TOML file of the
/// operator's own, <c>preferences.toml</c> in the app's data folder (the
/// owner, 2026-11-12: "The Desktop version can have a local TOML file, like
/// for cookies") — the <c>[bar]</c> table's <c>order</c> and the
/// <c>[theme]</c> table's <c>choice</c>, each holding the words the rules
/// write. The file is edited in place, so anything else in it stays as
/// written; a file that is not TOML is read as the defaults and replaced
/// when something is kept.
/// </summary>
public sealed class PreferenceFile(string path) : IPreferenceStore
{
    /// <summary>The file's name in the app's data folder.</summary>
    public const string Name = "preferences.toml";

    private readonly Lock guard = new();

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<BarTab> Order
    {
        get
        {
            lock (guard)
            {
                return BarOrder.Read(Kept("bar", "order"));
            }
        }
    }

    /// <inheritdoc />
    public Theme Theme
    {
        get
        {
            lock (guard)
            {
                return ThemeChoice.Read(Kept("theme", "choice"));
            }
        }
    }

    /// <inheritdoc />
    public void Keep(IReadOnlyList<BarTab>? order)
    {
        Write("bar", "order", order is null ? null : BarOrder.Say(order));
    }

    /// <inheritdoc />
    public void Keep(Theme theme)
    {
        Write("theme", "choice", theme == Theme.System ? null : ThemeChoice.Word(theme));
    }

    // One key of one table set, or removed where it is null, and the file
    // written; then whatever draws it is told.
    private void Write(string table, string key, string? value)
    {
        lock (guard)
        {
            DocumentSyntax document = Document() ?? TomlDocument.Parse(string.Empty);
            TableSyntax? found = TomlDocument.Tables<TableSyntax>(document, table).FirstOrDefault();

            if (found is null && value is not null)
            {
                found = new TableSyntax();
                TomlDocument.Insert(document, found, table);
            }

            if (found is not null)
            {
                TomlDocument.Set(found, key, value);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, document.ToString());
        }

        Changed?.Invoke();
    }

    // What one key of one table holds; null where the file, the table or
    // the key is absent, or the file is not TOML.
    private string? Kept(string table, string key)
    {
        return Document() is { } document
            && TomlDocument.Tables<TableSyntax>(document, table).FirstOrDefault() is { } found
            ? TomlDocument.Text(found, key)
            : null;
    }

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
}
