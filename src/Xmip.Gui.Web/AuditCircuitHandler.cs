using Microsoft.AspNetCore.Components.Server.Circuits;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Web;

/// <summary>
/// A viewer's circuit, audited (ADR-0062): opened, its connection lost and
/// regained, closed. A circuit that dies of an exception is logged by the
/// framework at Error and audited by <see cref="Hosting.AuditLoggerProvider"/>;
/// this says who was connected when it did.
/// </summary>
/// <param name="audit">The host's audit.</param>
internal sealed class AuditCircuitHandler(ProgramAudit audit) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        return Said(circuit, "circuit opened", AuditPhase.Begin, AuditSeverity.Information);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        return Said(circuit, "connection down", AuditPhase.Execute, AuditSeverity.Warning);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        return Said(circuit, "connection up", AuditPhase.Execute, AuditSeverity.Information);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        return Said(circuit, "circuit closed", AuditPhase.Finished, AuditSeverity.Information);
    }

    private Task Said(Circuit circuit, string action, AuditPhase phase, AuditSeverity severity)
    {
        audit.Record(action, phase, severity, null, new Dictionary<string, string>
        {
            ["circuit"] = circuit.Id,
        });

        return Task.CompletedTask;
    }
}
