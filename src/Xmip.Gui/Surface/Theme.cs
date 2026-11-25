namespace Xmip.Gui.Surface;

/// <summary>How the views are painted: as the operating system says, light
/// or dark (ADR-0052, amendment 2026-11-12).</summary>
public enum Theme
{
    /// <summary>As the operating system says, light or dark.</summary>
    System,

    /// <summary>Dark text on light paper.</summary>
    Light,

    /// <summary>Light text on dark paper.</summary>
    Dark,
}

/// <summary>
/// The one rule for the operator's theme (the owner, 2026-11-12: "Themes are
/// missing, I like Dark Mode. Make it a choice"): its word, as a store keeps
/// it and the stylesheet reads it, what a word kept is read as, and its name,
/// as Settings offers it (ADR-0052, amendment 2026-11-25). A word the rule
/// does not know is the system's, so a theme added later is never an error.
/// </summary>
public static class ThemeChoice
{
    /// <summary>The cookie a browser keeps the theme in.</summary>
    public const string Cookie = "xmip-theme";

    /// <summary>Its word: what a store keeps and <c>data-theme</c>
    /// says.</summary>
    public static string Word(Theme theme)
    {
        return theme switch
        {
            Theme.Light => "light",
            Theme.Dark => "dark",
            Theme.System or _ => "system",
        };
    }

    /// <summary>The theme <paramref name="said"/> names; the system's where
    /// it names none.</summary>
    public static Theme Read(string? said)
    {
        return said?.Trim() switch
        {
            "light" => Theme.Light,
            "dark" => Theme.Dark,
            _ => Theme.System,
        };
    }

    /// <summary>Its name, as Settings offers it.</summary>
    public static string Name(Theme theme)
    {
        return theme switch
        {
            Theme.Light => "Light",
            Theme.Dark => "Dark",
            Theme.System or _ => "System",
        };
    }

    /// <summary>What the Web host keeps for a theme a browser asked for:
    /// null, to delete the cookie, for the system's.</summary>
    public static string? Kept(string? asked)
    {
        Theme theme = Read(asked);

        return theme == Theme.System ? null : Word(theme);
    }
}
