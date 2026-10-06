namespace Xmip.Gui.Test;

/// <summary>
/// Where the audit of a view rendered for an Observer would go. Every view
/// that offers an act takes the host's audit, so the act passes the one gate
/// and is recorded (ADR-0062); a test whose Observer is offered none writes
/// nothing there.
/// </summary>
internal static class Unacted
{
    /// <summary>A directory no act of these tests writes into.</summary>
    public static string Audit { get; } =
        Path.Combine(Path.GetTempPath(), $"xmip-gui-unacted-{Guid.NewGuid():n}");
}
