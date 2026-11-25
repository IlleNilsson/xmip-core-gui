using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// A view's act on a Message that failed — at a Send Port (runtime-model.md
/// section 13) or a Work Process (section 22; ADR-0066, amendment
/// 2026-11-25) — written once for every view that offers one: through the
/// one gate as the proven caller (ADR-0009, amendment 2026-10-06), which
/// refuses one whose role may not act; recorded in this host's audit with
/// what came of it (ADR-0062) under the action <c>observe::Noun::Message</c>
/// gives it, <c>message.retry</c> or <c>message.dismiss</c>; and said.
/// </summary>
public static class MessageActing
{
    /// <summary>
    /// <paramref name="act"/> on the Message of the Journey
    /// <paramref name="at"/> names at its scope — the node that runs its Work
    /// Process, or the Port's scope — through <paramref name="surface"/>, as
    /// the role <paramref name="by"/> holds it, recorded in its audit: what
    /// the view says of it.
    /// </summary>
    public static string Act(
        IOperatorSurface surface,
        (RoleContext Role, ProgramAudit Audited) by,
        (string Scope, string Journey) at,
        MessageAct act)
    {
        ArgumentNullException.ThrowIfNull(by.Role);
        ArgumentNullException.ThrowIfNull(by.Audited);

        string action = $"{MessageOperation.Noun}.{MessageOperation.Word(act)}";
        string who = by.Role.Who ?? string.Empty;
        MessageOperation done = new GatedOperator(surface, by.Role, by.Audited)
            .Act(at.Scope, at.Journey, act, who, by.Role.Caller);

        by.Audited.Record(
            action,
            ActSaid.Recorded(done.Outcome).Phase,
            ActSaid.Recorded(done.Outcome).Severity,
            done.Result,
            new Dictionary<string, string>
            {
                ["node"] = done.Node,
                ["journey"] = at.Journey,
                ["who"] = who,
            });

        return ActSaid.Answer(done.Outcome, done.Result);
    }
}
