using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// The classes an element carries for a mood, so the stylesheet paints it
/// without knowing which mood is which color (ADR-0041, amendment
/// 2026-09-14): the mood's word, for what a page does with one mood alone —
/// a dashed line for a Done link — and the name of the mood's color, which
/// the stylesheet renders to its token. Both are the runtime's
/// (<c>observe::Health</c>, called through <see cref="English"/>); the
/// stylesheet held its own mood-to-color table until 2026-09-24.
/// </summary>
public static class MoodClass
{
    /// <summary>The classes for one mood: <c>done red</c>.</summary>
    public static string Of(HealthState state)
    {
        return $"{English.Mood(state)} {English.Color(state)}";
    }

    /// <summary>The classes for the rollup over a set of leaves, or
    /// <c>none</c> when nothing is recorded — nothing is not Fine.</summary>
    public static string Of(IEnumerable<HealthRecord> records)
    {
        return ScopeTree.Rollup(records) is HealthState rolled ? Of(rolled) : "none";
    }
}
