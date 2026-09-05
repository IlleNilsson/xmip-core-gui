using Microsoft.Extensions.Configuration;
using Tomlyn.Extensions.Configuration;

namespace Xmip.Gui.Surface;

/// <summary>
/// Reads a snapshot the Xmip Playground writes after every tick, so the
/// monitoring UI shows the pingpong matrix rolling over time. The playground
/// and the GUI are two long-running processes; the file is the bridge, read
/// fresh on every query so each render reflects the latest round.
/// </summary>
/// <remarks>
/// The file is TOML — on disk the estate is TOML, and JSON is reserved for
/// memory and the wire — read with the same TOML reader the hosts use for
/// configuration. A read-only surface: it reports what the playground published
/// and never acts on it, so <see cref="PauseScope"/> and <see cref="ResumeScope"/>
/// decline. ADR-0027, ADR-0028, ADR-0029.
/// </remarks>
public sealed class FileOperator(string path) : IOperatorSurface
{
    /// <summary>The well-known snapshot path, shared with the playground's own
    /// default so the two agree with no configuration.</summary>
    public static string DefaultPath { get; } =
        Path.Combine(Path.GetTempPath(), "playground-snapshot.toml");

    /// <inheritdoc />
    public string Source => $"PLAYGROUND — {path}";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        Snapshot snapshot = Read();

        return
        [
            .. snapshot.Records
                .Where(record => Beneath(record.Scope, scope))
                .OrderByDescending(record => record.State)
                .ThenByDescending(record => record.Severity)
                .ThenBy(record => record.Scope, StringComparer.Ordinal),
        ];
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        Snapshot snapshot = Read();

        ulong value = snapshot.Counts
            .Where(count => count.Counted == counted && Beneath(count.Scope, scope))
            .Aggregate(0UL, (sum, count) => sum + count.Value);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new MeasurementRecord(scope, counted, value, now.AddMinutes(-1), now, now);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return "the playground is a test surface and cannot be paused";
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return "the playground is a test surface and cannot be resumed";
    }

    private sealed record Snapshot(
        IReadOnlyList<HealthRecord> Records,
        IReadOnlyList<CountRecord> Counts);

    private sealed record CountRecord(string Scope, Counted Counted, ulong Value);

    /// <summary>Parse the file each call. A missing or half-written file (the
    /// playground writes atomically, but it may not have ticked yet) reads as an
    /// empty snapshot rather than an error, so the page shows nothing rather than
    /// breaking.</summary>
    private Snapshot Read()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new Snapshot([], []);
            }

            IConfigurationRoot document = new ConfigurationBuilder()
                .AddTomlFile(path, optional: true, reloadOnChange: false)
                .Build();

            string node = document["node"] ?? "xmip:///";

            List<HealthRecord> records = [];
            foreach (IConfigurationSection row in document.GetSection("records").GetChildren())
            {
                records.Add(new HealthRecord(
                    row["scope"] ?? string.Empty,
                    ParseState(row["state"]),
                    ParseByte(row["severity"]),
                    row["evidence"] ?? string.Empty,
                    ParseObserved(row["observed_unix_nanos"])));
            }

            List<CountRecord> counts = [];
            foreach (IConfigurationSection row in document.GetSection("counts").GetChildren())
            {
                counts.Add(new CountRecord(node, ParseCounted(row["counted"]), ParseUlong(row["value"])));
            }

            return new Snapshot(records, counts);
        }
        catch (Exception exception)
            when (exception is IOException or FormatException or InvalidOperationException)
        {
            return new Snapshot([], []);
        }
    }

    private static HealthState ParseState(string? state)
    {
        return state switch
        {
            "green" => HealthState.Green,
            "yellow" => HealthState.Yellow,
            "red" => HealthState.Red,
            _ => HealthState.Yellow,
        };
    }

    private static Counted ParseCounted(string? counted)
    {
        return counted switch
        {
            "streams" => Counted.Streams,
            "messages" => Counted.Messages,
            "journeys" => Counted.Journeys,
            "bytes" => Counted.Bytes,
            _ => Counted.Streams,
        };
    }

    private static byte ParseByte(string? value)
    {
        return byte.TryParse(value, out byte parsed) ? parsed : (byte)0;
    }

    private static ulong ParseUlong(string? value)
    {
        return ulong.TryParse(value, out ulong parsed) ? parsed : 0UL;
    }

    private static DateTimeOffset ParseObserved(string? nanos)
    {
        return long.TryParse(nanos, out long value)
            ? DateTimeOffset.UnixEpoch.AddTicks(value / 100)
            : DateTimeOffset.UtcNow;
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
