using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// What a subscriptions view says of an act, written once for both kinds —
/// the Subscriptions (ADR-0013, amendment 2026-09-30) and the Event
/// subscriptions (ADR-0065, amendment 2026-09-29): where an act goes, what
/// came of it, and the refusal a role that only watches is given.
/// </summary>
public static class ActSaid
{
    /// <summary>
    /// Where an act goes, said once: a snapshot's publisher takes orders, and
    /// the act is left for <paramref name="holder"/>; or it takes none; a
    /// live surface acts in <paramref name="live"/>.
    /// </summary>
    public static string Where(IOperatorSurface surface, string orders, string holder, string live)
    {
        return surface is SnapshotOperator
            ? orders.Length > 0
                ? $"an act is left for {holder}, which takes it within a round"
                : "this publication takes no act: its publisher reads no orders"
            : $"an act is applied in {live}";
    }

    /// <summary>What came of an act, as the view says it: what was applied
    /// as the surface said it, and anything else as a refusal.</summary>
    public static string Answer(bool applied, string result)
    {
        return applied || result.StartsWith("REFUSED", StringComparison.Ordinal)
            ? result
            : $"REFUSED: {result}";
    }

    /// <summary>The act a role that may not act reached anyway.</summary>
    public static string Refused(Role role, string word, string what)
    {
        return $"REFUSED: an {role} watches; to {word} {what} is an Operator's.";
    }
}
