using Microsoft.AspNetCore.Components;

namespace Xmip.Gui.Surface;

/// <summary>
/// The Web host's store of the bar's order: the browser's cookie
/// (<see cref="BarOrder.Cookie"/>). The host reads it as a page is asked
/// for and hands it to the circuit (<see cref="Said"/>), so the bar is drawn
/// in its order from the first paint. A circuit cannot write a cookie, so
/// keeping an order asks the host's own address for it
/// (<see cref="Address"/>), which writes the cookie and comes back here: a
/// page load, as rare as arranging the bar. No script of Xmip's own is
/// involved: JavaScript lives in the VS Code extension alone.
/// </summary>
public sealed class BarOrderCookie(NavigationManager navigation) : IBarOrderStore
{
    /// <summary>The host's address that keeps an order.</summary>
    public const string Address = "bar";

    /// <summary>What the browser's cookie held when the page was asked
    /// for; null where it held nothing.</summary>
    public string? Said { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<BarTab> Order => BarOrder.Read(Said);

    /// <inheritdoc />
    public void Keep(IReadOnlyList<BarTab>? order)
    {
        string said = order is null ? string.Empty : BarOrder.Say(order);
        string back = "/" + navigation.ToBaseRelativePath(navigation.Uri);

        navigation.NavigateTo(
            $"{Address}?order={Uri.EscapeDataString(said)}&back={Uri.EscapeDataString(back)}",
            forceLoad: true);
    }
}
