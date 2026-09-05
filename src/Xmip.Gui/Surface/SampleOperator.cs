namespace Xmip.Gui.Surface;

/// <summary>
/// Stands in when no runtime library can be loaded, so the screens have a
/// shape to show. <see cref="Source"/> says so and every page prints it —
/// a stand-in that could be mistaken for a node is worse than an empty page.
/// </summary>
public sealed class SampleOperator(string reason) : IOperatorSurface
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // xmip:///<node>/<receive|process|send>/<name>: the node is where it runs,
    // the stage is what it is. The landing page groups by stage. Severity
    // shades the colour — a red at 95 is worse than one at 70, and the sample
    // shows the spread so the shading is visible without a running node.
    private readonly Dictionary<string, HealthRecord> _tree = new()
    {
        ["xmip:///edge-01/receive/partner-x-sftp"] = new("xmip:///edge-01/receive/partner-x-sftp",
            HealthState.Red, 95, "connection refused by partner-x (10.0.4.21:22)", Now.AddSeconds(-4)),
        ["xmip:///edge-01/process/approval"] = new("xmip:///edge-01/process/approval",
            HealthState.Yellow, 55, "3 Journeys waiting longer than 5 minutes", Now.AddSeconds(-2)),
        ["xmip:///edge-01/receive/orders-ftp"] = new("xmip:///edge-01/receive/orders-ftp",
            HealthState.Green, 0, "", Now.AddSeconds(-2)),
        ["xmip:///edge-01/receive/orders-http"] = new("xmip:///edge-01/receive/orders-http",
            HealthState.Green, 0, "", Now.AddSeconds(-1)),
        ["xmip:///edge-01/send/billing"] = new("xmip:///edge-01/send/billing",
            HealthState.Green, 0, "", Now.AddSeconds(-1)),
        ["xmip:///edge-02/send/warehouse"] = new("xmip:///edge-02/send/warehouse",
            HealthState.Red, 70, "no answer in 10 s", Now.AddSeconds(-10)),
    };

    /// <inheritdoc />
    public string Source { get; } = $"SAMPLE — no node loaded ({reason})";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return
        [
            .. _tree.Values
                .Where(record => Beneath(record.Scope, scope))
                .OrderByDescending(record => record.State)
                .ThenByDescending(record => record.Severity)
                .ThenBy(record => record.Scope, StringComparer.Ordinal),
        ];
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        ulong value = counted switch
        {
            Counted.Streams => 1_284UL,
            Counted.Messages => 1_284UL,
            Counted.Journeys => 2_110UL,
            Counted.Bytes => 91_337_412UL,
            _ => 0UL,
        };

        return new MeasurementRecord(scope, counted, value, Now.AddMinutes(-1), Now, Now);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        int paused = 0;

        foreach (string key in _tree.Keys.Where(k => Beneath(k, scope)).ToList())
        {
            HealthRecord was = _tree[key];
            _tree[key] = was with { State = HealthState.Yellow, Severity = 30, Evidence = $"paused by {who}" };
            paused++;
        }

        return paused == 0 ? $"nothing to pause at {scope}" : $"paused {scope}";
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        // The stand-in has nowhere to restore from, so resume clears the pause
        // to a plain green. The real surface puts back exactly what was there.
        int resumed = 0;

        foreach (string key in _tree.Keys.Where(k => Beneath(k, scope)).ToList())
        {
            if (_tree[key].Evidence.StartsWith("paused by", StringComparison.Ordinal))
            {
                _tree[key] = _tree[key] with { State = HealthState.Green, Severity = 0, Evidence = "" };
                resumed++;
            }
        }

        return resumed == 0 ? $"nothing to resume at {scope}" : $"resumed {scope}";
    }

    private static bool Beneath(string candidate, string scope)
    {
        string trimmed = scope.TrimEnd('/');

        return candidate == trimmed
            || (candidate.StartsWith(trimmed, StringComparison.Ordinal)
                && candidate.Length > trimmed.Length
                && candidate[trimmed.Length] == '/');
    }
}
