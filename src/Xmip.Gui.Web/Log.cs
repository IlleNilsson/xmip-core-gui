namespace Xmip.Gui.Web;

/// <summary>
/// Source-generated log messages. CA1848 in the template's analysis level:
/// a formatted string per call allocates on every call, and a generated
/// delegate does not.
/// </summary>
internal static partial class Log
{
    /// <summary>The runtime library could not be loaded, so a stand-in is
    /// answering. Warning, because every page says so too.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Showing sample data: {Reason}")]
    public static partial void ShowingSample(this ILogger logger, string reason);
}
