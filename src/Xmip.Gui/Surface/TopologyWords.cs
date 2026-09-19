using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// How the communication topology says what it draws. The graph names a
/// node's kind under the node and the inspector names it in a row; the legend
/// names a communication pattern and the inspector heads a selected link with
/// it. One place writes those words, so two views of the same thing cannot
/// disagree about what it is called.
/// </summary>
public static class TopologyWords
{
    /// <summary>A value, or an em dash where the publisher said none.</summary>
    public static string Value(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    /// <summary>A fraction of one as a whole percentage, clamped to its range.</summary>
    public static string Percent(double value)
    {
        return $"{Math.Clamp(value, 0D, 1D):P0}";
    }

    /// <summary>A health state in the estate's own word for it.</summary>
    public static string Mood(HealthState state)
    {
        return English.Mood(state);
    }

    /// <summary>An origin as the class that styles it.</summary>
    public static string Origin(TopologyOrigin origin)
    {
        return origin.ToString().ToLowerInvariant();
    }

    /// <summary>An origin in words: what is configured, observed, or both.</summary>
    public static string OriginName(TopologyOrigin origin)
    {
        return origin switch
        {
            TopologyOrigin.Configured => "configured",
            TopologyOrigin.Observed => "observed",
            _ => "configured and observed",
        };
    }

    /// <summary>A node's kind in words.</summary>
    public static string Kind(TopologyNodeKind kind)
    {
        return kind switch
        {
            TopologyNodeKind.VirtualMachine => "virtual machine",
            _ => kind.ToString().ToLowerInvariant(),
        };
    }

    /// <summary>A communication pattern as the class that styles it.</summary>
    public static string Pattern(CommunicationPattern pattern)
    {
        return pattern.ToString().ToLowerInvariant();
    }

    /// <summary>A communication pattern in words, as the legend reads it.</summary>
    public static string PatternName(CommunicationPattern pattern)
    {
        return pattern switch
        {
            CommunicationPattern.RequestResponse => "Request / response",
            CommunicationPattern.SendReceive => "Send → receive",
            CommunicationPattern.PublishConsume => "Publish → consume",
            CommunicationPattern.FireAndForget => "Fire-and-forget",
            _ => pattern.ToString().ToLowerInvariant(),
        };
    }
}
