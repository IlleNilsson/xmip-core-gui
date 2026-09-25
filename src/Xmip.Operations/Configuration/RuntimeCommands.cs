using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Operations.Configuration;

/// <summary>
/// The commands the desktop can run that a browser cannot: asking the native
/// runtime to validate or start a node configuration. A browser is sandboxed —
/// no native library, no local file to hand it — which is exactly why
/// configuration and node control live in the desktop GUI, not the web one
/// (ADR-0014, amendment of 2026-09-05). When the board reads the runtime,
/// these go through that same runtime; when it reads a snapshot, the runtime
/// is loaded here alone, from the library the one discovery rule found. Each
/// answers with the <see cref="ConfigurationVerdict"/> — the record and the
/// sentence — so the page decides from the status and shows the words
/// (ADR-0052 clause 4). Both are an operator's acts and are audited, with
/// what the runtime answered (ADR-0062).
/// </summary>
public sealed class RuntimeCommands(
    IOperatorSurface surface, string libraryPath, ProgramAudit audit)
{
    private NativeOperator? _own;

    /// <summary>Validate the document the editor is holding through the native
    /// runtime, saved or not (ADR-0027, amendment 2026-09-05): the runtime's
    /// answer is the only one the editor gives.</summary>
    public ConfigurationVerdict Validate(string configurationPath, string configuration)
    {
        return Audited("validate", Runtime().Validate(configurationPath, configuration));
    }

    /// <summary>Start a node from its configuration through the native runtime.</summary>
    public ConfigurationVerdict Start(string configurationPath)
    {
        return Audited("start node", Runtime().Start(configurationPath));
    }

    // The act and what came of it, one record: Finished when the runtime did
    // what was asked, Failure with its sentence when it did not.
    private ConfigurationVerdict Audited(string action, ConfigurationVerdict verdict)
    {
        audit.Record(
            action,
            verdict.Ok ? AuditPhase.Finished : AuditPhase.Failure,
            verdict.Ok ? AuditSeverity.Information : AuditSeverity.Error,
            verdict.Said,
            new Dictionary<string, string>
            {
                ["configuration"] = verdict.Path,
                ["status"] = $"{verdict.Status}",
                ["problems"] = string.Join("; ", verdict.Problems),
            });

        return verdict;
    }

    private NativeOperator Runtime()
    {
        return surface as NativeOperator ?? (_own ??= new NativeOperator(libraryPath));
    }
}
