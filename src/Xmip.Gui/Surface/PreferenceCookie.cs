using Microsoft.AspNetCore.Components;

namespace Xmip.Gui.Surface;

/// <summary>
/// The Web host's store of the operator's view: the browser's cookies, the
/// bar's order (<see cref="BarOrder.Cookie"/>) and the theme
/// (<see cref="ThemeChoice.Cookie"/>). The host reads them as a page is
/// asked for and hands them to the circuit (<see cref="SaidBar"/>,
/// <see cref="SaidTheme"/>), so the page is drawn in them from the first
/// paint. A circuit cannot write a cookie, so keeping asks the host's own
/// address for it (<see cref="Address"/>), which writes the cookie and comes
/// back here: a page load, as rare as arranging the bar or choosing a theme.
/// No script of Xmip's own is involved: JavaScript lives in the VS Code
/// extension alone.
/// </summary>
public sealed class PreferenceCookie(NavigationManager navigation) : IPreferenceStore
{
    /// <summary>The host's address that keeps what a browser chose.</summary>
    public const string Address = "preferences";

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>What the bar's cookie held when the page was asked for;
    /// null where it held nothing.</summary>
    public string? SaidBar { get; set; }

    /// <summary>What the theme's cookie held when the page was asked for;
    /// null where it held nothing.</summary>
    public string? SaidTheme { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<BarTab> Order => BarOrder.Read(SaidBar);

    /// <inheritdoc />
    public Theme Theme => ThemeChoice.Read(SaidTheme);

    /// <summary>Where the host sends a browser back to once it kept a
    /// choice: <paramref name="back"/> where it is a path on this host, the
    /// bare address otherwise, never another host's address.</summary>
    public static string Back(string? back)
    {
        bool local = back is { Length: > 0 }
            && back[0] == '/'
            && !back.StartsWith("//", StringComparison.Ordinal)
            && !back.StartsWith("/\\", StringComparison.Ordinal)
            && !back.Contains("://", StringComparison.Ordinal);

        return local ? back! : "/";
    }

    /// <inheritdoc />
    public void Keep(IReadOnlyList<BarTab>? order)
    {
        Ask("bar", order is null ? string.Empty : BarOrder.Say(order));
    }

    /// <inheritdoc />
    public void Keep(Theme theme)
    {
        Ask("theme", ThemeChoice.Word(theme));
    }

    // The host's address asked to keep one choice, and to come back here.
    private void Ask(string what, string said)
    {
        string back = "/" + navigation.ToBaseRelativePath(navigation.Uri);

        Changed?.Invoke();
        navigation.NavigateTo(
            $"{Address}?{what}={Uri.EscapeDataString(said)}&back={Uri.EscapeDataString(back)}",
            forceLoad: true);
    }
}
