using System.Text.Json;

namespace Xmip.Gui.Surface;

/// <summary>
/// Reads a snapshot the Xmip Playground writes after every tick, so the
/// monitoring UI shows the pingpong matrix rolling over time. The playground
/// and the GUI are two long-running processes; the file is the bridge, read
/// fresh on every query so each render reflects the latest round.
/// </summary>
/// <remarks>
/// A read-only surface: it reports what the playground published and never acts
/// on it, so <see cref="PauseScope"/> and <see cref="ResumeScope"/> decline —
/// the playground is a test, not a node to mitigate. ADR-0027, ADR-0028.
/// </remarks>
public sealed class FileOperator(string path) : IOperatorSurface
{
    /// <summary>The well-known snapshot path, shared with the playground's own
    /// default so the two agree with no configuration.</summary>
    public static string DefaultPath { get; } =
        Path.Combine(Path.GetTempPath(), "xmip-playground-snapshot.json");

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
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;

            List<HealthRecord> records = [];
            if (root.TryGetProperty("records", out JsonElement recordsElement))
            {
                foreach (JsonElement item in recordsElement.EnumerateArray())
                {
                    records.Add(ReadHealth(item));
                }
            }

            List<CountRecord> counts = [];
            if (root.TryGetProperty("counts", out JsonElement countsElement))
            {
                foreach (JsonElement item in countsElement.EnumerateArray())
                {
                    counts.Add(new CountRecord(
                        root.GetProperty("node").GetString() ?? "xmip:///",
                        ParseCounted(item.GetProperty("counted").GetString()),
                        item.GetProperty("value").GetUInt64()));
                }
            }

            return new Snapshot(records, counts);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return new Snapshot([], []);
        }
    }

    private static HealthRecord ReadHealth(JsonElement item)
    {
        long nanos = item.GetProperty("observedUnixNanos").GetInt64();
        DateTimeOffset observed = DateTimeOffset.UnixEpoch.AddTicks(nanos / 100);

        return new HealthRecord(
            item.GetProperty("scope").GetString() ?? "",
            ParseState(item.GetProperty("state").GetString()),
            item.GetProperty("severity").GetByte(),
            item.GetProperty("evidence").GetString() ?? "",
            observed);
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

    private static bool Beneath(string candidate, string scope)
    {
        string trimmed = scope.TrimEnd('/');

        return candidate == trimmed
            || (candidate.StartsWith(trimmed, StringComparison.Ordinal)
                && candidate.Length > trimmed.Length
                && candidate[trimmed.Length] == '/');
    }
}
