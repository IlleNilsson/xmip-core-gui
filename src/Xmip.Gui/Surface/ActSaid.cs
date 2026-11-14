using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// What a view that acts on what a node publishes says of an act, written
/// once for every kind — the Subscriptions (ADR-0013, amendment 2026-09-30),
/// the Event subscriptions (ADR-0065, amendment 2026-09-29) and the Dead
/// Message Queue (ADR-0052, amendment 2026-10-01): where an act goes and what
/// came of it.
/// </summary>
public static class ActSaid
{
    /// <summary>
    /// Where an act goes, said once: a snapshot is never acted on; a live
    /// surface asks it at a node's door, and <paramref name="holder"/> takes
    /// it (ADR-0014, amendment 2026-11-13).
    /// </summary>
    public static string Where(IOperatorSurface surface, string holder)
    {
        return surface is SnapshotOperator
            ? "this publication takes no act: a snapshot is never acted on"
            : $"an act is asked at a node's door and taken by {holder}";
    }

    /// <summary>What came of an act, as the view says it: what was applied
    /// as the surface said it, and anything else as a refusal.</summary>
    public static string Answer(bool applied, string result)
    {
        return Answer(applied ? ActOutcome.Taken : ActOutcome.Refused, result);
    }

    /// <summary>What came of an act at a node's door, as the view says it:
    /// taken, and asked — <c>asked of</c> the node, <c>not taken yet</c> — as the node
    /// said them (ADR-0014, amendment 2026-11-13), and anything else as a
    /// refusal.</summary>
    public static string Answer(ActOutcome outcome, string result)
    {
        return outcome != ActOutcome.Refused
            || result.StartsWith("REFUSED", StringComparison.Ordinal)
                ? result
                : $"REFUSED: {result}";
    }

    /// <summary>The audit phase and severity a view records an act that came
    /// to <paramref name="outcome"/> with: asked is no failure.</summary>
    public static (AuditPhase Phase, AuditSeverity Severity) Recorded(ActOutcome outcome)
    {
        return outcome == ActOutcome.Refused
            ? (AuditPhase.Failure, AuditSeverity.Warning)
            : (AuditPhase.Execute, AuditSeverity.Information);
    }
}
