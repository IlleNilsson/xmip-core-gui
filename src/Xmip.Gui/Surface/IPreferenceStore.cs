namespace Xmip.Gui.Surface;

/// <summary>
/// Where the operator's own view is kept: the order of the bar's views and
/// the theme (ADR-0052, amendments 2026-11-12) — a cookie for the Web host's
/// browser (<see cref="PreferenceCookie"/>), a local TOML file for the
/// Desktop (<see cref="PreferenceFile"/>). Each keeps the words the rules
/// write (<see cref="BarOrder.Say"/>, <see cref="ThemeChoice.Word"/>) and
/// reads them back by the same rules.
/// </summary>
public interface IPreferenceStore
{
    /// <summary>Raised once something kept changed, so what draws it draws
    /// it again.</summary>
    public event Action? Changed;

    /// <summary>The order kept, as the operator arranged it, or the
    /// default.</summary>
    public IReadOnlyList<BarTab> Order { get; }

    /// <summary>The theme kept, or the system's.</summary>
    public Theme Theme { get; }

    /// <summary>Keep <paramref name="order"/>; null goes back to the
    /// default.</summary>
    public void Keep(IReadOnlyList<BarTab>? order);

    /// <summary>Keep <paramref name="theme"/>.</summary>
    public void Keep(Theme theme);
}
