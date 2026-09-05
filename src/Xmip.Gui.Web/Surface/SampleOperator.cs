namespace Xmip.Gui.Web.Surface;

/// <summary>
/// Stands in when no runtime library can be loaded, so the screens have a
/// shape to show. <see cref="Source"/> says so and every page prints it —
/// a stand-in that could be mistaken for a node is worse than an empty page.
/// </summary>
public sealed class SampleOperator(string reason) : IOperatorSurface
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static readonly HealthRecord[] Tree =
    [
        // xmip:///<node>/<receive|process|send>/<name>: the node is where it
        // runs, the stage is what it is. The landing page groups by stage.
        new("xmip:///edge-01/receive/partner-x-sftp", HealthState.Red,
            "connection refused by partner-x (10.0.4.21:22)", Now.AddSeconds(-4)),
        new("xmip:///edge-01/process/approval", HealthState.Yellow,
            "3 Journeys waiting longer than 5 minutes", Now.AddSeconds(-2)),
        new("xmip:///edge-01/receive/orders-ftp", HealthState.Green, "", Now.AddSeconds(-2)),
        new("xmip:///edge-01/receive/orders-http", HealthState.Green, "", Now.AddSeconds(-1)),
        new("xmip:///edge-01/send/billing", HealthState.Green, "", Now.AddSeconds(-1)),
        new("xmip:///edge-02/send/warehouse", HealthState.Unreachable, "no answer in 10 s", Now.AddSeconds(-10)),
    ];

    /// <inheritdoc />
    public string Source { get; } = $"SAMPLE — no node loaded ({reason})";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return
        [
            .. Tree
                .Where(record => Beneath(record.Scope, scope))
                .OrderByDescending(record => record.State)
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

    private static bool Beneath(string candidate, string scope)
    {
        string trimmed = scope.TrimEnd('/');

        return candidate == trimmed
            || (candidate.StartsWith(trimmed, StringComparison.Ordinal)
                && candidate.Length > trimmed.Length
                && candidate[trimmed.Length] == '/');
    }
}
