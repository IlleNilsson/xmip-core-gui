namespace Xmip.Gui.Surface;

/// <summary>
/// What every screen reads. One implementation crosses the C ABI in
/// <c>xmip_operate.h</c>; the other stands in when no runtime is loaded and
/// says so on every record it returns.
/// </summary>
/// <remarks>
/// ADR-0027: a surface reads snapshots the runtime published and never asks
/// the hot path. Nothing here can make a node wait.
/// </remarks>
public interface IOperatorSurface
{
    /// <summary>Where the records come from, shown on screen so nobody
    /// mistakes a stand-in for a node.</summary>
    public string Source { get; }

    /// <summary>Health at and beneath a scope, worst first, most severe first
    /// within a state.</summary>
    public IReadOnlyList<HealthRecord> Health(string scope);

    /// <summary>One kind of count, summed over the scope.</summary>
    public MeasurementRecord? Measure(string scope, Counted counted);

    /// <summary>Pause everything at and beneath a scope, by <paramref name="who"/>.
    /// The first operation that acts rather than reads. Returns what the runtime
    /// said, for the operator to see.</summary>
    public string PauseScope(string scope, string who);

    /// <summary>Resume everything at and beneath a scope.</summary>
    public string ResumeScope(string scope);
}

/// <summary>
/// The mood of a scope — observability-model.md section 6. A mood, not a colour
/// (the surface paints it): what a human gets out of a resource under load. Five
/// leaf moods, worsening, then Holding, the rollup (ADR-0041).
/// </summary>
public enum HealthState
{
    /// <summary>Results flowing, at ease.</summary>
    Fine = 0,

    /// <summary>A deliberate hold — an operator is working on it.</summary>
    Paused = 1,

    /// <summary>Handling the load.</summary>
    Working = 2,

    /// <summary>Strained — change the load.</summary>
    Stressed = 3,

    /// <summary>Spent — replace the hardware.</summary>
    Exhausted = 4,

    /// <summary>Blocked or failed — the pain (a cert, a password, a folder).</summary>
    Done = 5,

    /// <summary>Rollup only: a parent with something not-Fine beneath it.</summary>
    Holding = 6,
}

/// <summary>What a measurement counts. Never a bare number.</summary>
public enum Counted
{
    /// <summary>Streams, at a Receive Location.</summary>
    Streams = 1,

    /// <summary>Messages, at a Send Location.</summary>
    Messages = 2,

    /// <summary>Journeys, in an Xmip Process.</summary>
    Journeys = 3,

    /// <summary>Bytes, wherever content moved.</summary>
    Bytes = 4,
}

/// <summary>One scope's health, how far from healthy, the line that explains
/// it, and when it was seen. Severity is 0–100: the mood says which, the number
/// shades it.</summary>
public sealed record HealthRecord(
    string Scope, HealthState State, byte Severity, string Evidence, DateTimeOffset Observed);

/// <summary>One count over a window, and when it was taken.</summary>
public sealed record MeasurementRecord(
    string Scope,
    Counted Counted,
    ulong Value,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset Observed);
