using Xmip.Abi.Operate;
using Xmip.Gui.Surface;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// An act at a node's door that its node did not take within the door's
/// bound is asked (ADR-0014, amendment 2026-11-13): the Web and the Desktop
/// say the node's own sentence, never a refusal, and audit it as no
/// failure; a refusal is said as one.
/// </summary>
public sealed class ActAskedTest
{
    private const string Asked = "asked of the node; not taken yet; it takes it as soon as it runs";

    [Fact]
    public void AnAskedActIsSaidAsTheNodeSaidItAndAuditedAsNoFailure()
    {
        Assert.Equal(Asked, ActSaid.Answer(ActOutcome.Asked, Asked));
        Assert.Equal(
            (AuditPhase.Execute, AuditSeverity.Information),
            ActSaid.Recorded(ActOutcome.Asked));
    }

    [Fact]
    public void ARefusalIsSaidAsOneAndATakenActAsItWasTaken()
    {
        Assert.Equal("REFUSED: gone", ActSaid.Answer(ActOutcome.Refused, "gone"));
        Assert.Equal("REFUSED: gone", ActSaid.Answer(ActOutcome.Refused, "REFUSED: gone"));
        Assert.Equal("paused", ActSaid.Answer(ActOutcome.Taken, "paused"));
        Assert.Equal(
            (AuditPhase.Failure, AuditSeverity.Warning),
            ActSaid.Recorded(ActOutcome.Refused));
    }
}
