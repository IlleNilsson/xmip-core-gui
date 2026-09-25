using Microsoft.Extensions.Logging;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Hosting;

/// <summary>
/// Every error a GUI host logs becomes an audit record (ADR-0062 clause 4: a
/// failure is never only on a screen). A Blazor circuit that dies — what the
/// browser shows as <em>An unhandled error has occurred</em> — is logged by
/// the framework at Error with its exception and nothing else; so is an
/// unhandled request, a failed start and a hub that threw. One provider,
/// added to each host's logging, catches them all and hands each to the
/// audit capability through <see cref="ProgramAudit"/>, which writes no
/// record of its own either.
/// </summary>
/// <remarks>
/// Error and Critical only: a host's logs are logs (observability-model
/// section 1), and audit records what happened, not what was traced. The
/// category and event travel as properties; nothing reads meaning from them.
/// </remarks>
/// <param name="audit">The host's audit.</param>
public sealed class AuditLoggerProvider(ProgramAudit audit) : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new AuditLogger(audit, categoryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class AuditLogger(ProgramAudit audit, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel is LogLevel.Error or LogLevel.Critical;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
            {
                return;
            }

            string logged = formatter(state, exception);
            Dictionary<string, string> properties = new()
            {
                ["category"] = category,
                ["event"] = eventId.Name ?? $"{eventId.Id}",
                ["logged"] = logged,
            };

            if (exception is not null)
            {
                audit.Failed("error logged", exception, properties);
            }
            else
            {
                audit.Record(
                    "error logged", AuditPhase.Failure, AuditSeverity.Error, logged, properties);
            }
        }
    }
}
