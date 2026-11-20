[assembly: AssemblyFixture(typeof(Xmip.Gui.Test.Unacted))]

namespace Xmip.Gui.Test;

/// <summary>
/// Where the audit of a view rendered for an Observer would go. Every view
/// that offers an act takes the host's audit, so the act passes the one gate
/// and is recorded (ADR-0062); a test whose Observer is offered none writes
/// nothing there. The folder it is named in is the assembly's: xUnit makes
/// this fixture before the first test and disposes it after the last.
/// </summary>
internal sealed class Unacted : IDisposable
{
    private static readonly TestFolder Folder = new("gui-unacted");

    /// <summary>A directory no act of these tests writes into.</summary>
    public static string Audit { get; } = Folder.Inside("audit");

    /// <inheritdoc />
    public void Dispose()
    {
        Folder.Dispose();
    }
}
