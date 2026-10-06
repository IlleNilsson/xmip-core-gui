using System.Runtime.CompilerServices;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// A surface a test composes (ADR-0068, clause 5): the failed Journeys it
/// answers are the test's, page by page, or none at all where
/// <see cref="Listing"/> is null — a surface that cannot list them. Every
/// act and every watch begun is counted; a watch says its first view and
/// ends, as a surface that gave up does.
/// </summary>
internal sealed class ScriptedSurface : IOperatorSurface
{
    /// <summary>What a question for the failed Journeys at a scope, from a
    /// place, is answered; null where this surface cannot list them.</summary>
    public Func<string, ulong, FailedJourneyList>? Listing { get; set; }

    /// <summary>Every place the failed Journeys were asked from, in order.</summary>
    public List<ulong> Asked { get; } = [];

    /// <summary>Every act taken, as <c>act journey</c>.</summary>
    public List<string> Acts { get; } = [];

    /// <summary>How many watches were begun.</summary>
    public int Watches { get; private set; }

    /// <inheritdoc />
    public string Source => "scripted";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return [];
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return null;
    }

    /// <inheritdoc />
    public FailedJourneyList FailedJourneys(string scope, ulong from = 0, uint most = 0)
    {
        Asked.Add(from);

        return Listing is { } listing ? listing(scope, from) : FailedJourneyList.Unlisted;
    }

    /// <inheritdoc />
    public JourneyOperation Act(string scope, string journey, JourneyAct act, string who)
    {
        Acts.Add($"{JourneyOperation.Word(act)} {journey}");

        return new JourneyOperation(scope, journey, act, true, "applied");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SurfaceChange> WatchAsync(
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        Watches++;
        await Task.CompletedTask.ConfigureAwait(false);

        yield return SurfaceChange.Initial(Source);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return string.Empty;
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return string.Empty;
    }
}
