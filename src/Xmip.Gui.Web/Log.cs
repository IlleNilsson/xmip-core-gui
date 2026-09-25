namespace Xmip.Gui.Web;

/// <summary>
/// Source-generated log messages. CA1848 in the template's analysis level:
/// a formatted string per call allocates on every call, and a generated
/// delegate does not.
/// </summary>
internal static partial class Log
{
    /// <summary>Which surface the page reads, as the page itself says it.</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Reading {Source}")]
    public static partial void ReadingSurface(this ILogger logger, string source);

    /// <summary>A plain address bound on loopback, the one exception to
    /// Xmip's own traffic being TLS (ADR-0063 clause 1).</summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Address} is plain HTTP on loopback: ADR-0063 clause 1's one exception")]
    public static partial void PlainLoopback(this ILogger logger, string address);
}
