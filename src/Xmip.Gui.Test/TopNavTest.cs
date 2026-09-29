using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Gui.Components;
using Xmip.Gui.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// One navigation for both hosts: the four views, what a host adds after
/// them, and the role described in one set of words. Until 2026-09-27 the web
/// host and the desktop each wrote the bar and worded the Observer
/// differently.
/// </summary>
public sealed class TopNavTest : BunitContext
{
    [Fact]
    public void TheBarLinksTheFourViewsAndDescribesTheRoleOnce()
    {
        Services.AddSingleton(new RoleContext(Role.Observer));

        IRenderedComponent<TopNav> bar = Render<TopNav>();

        Assert.Equal(
            ["/configuration", "/", "/topology", "/audit"],
            bar.FindAll("a.topnav-link").Select(link => link.GetAttribute("href")));
        AngleSharp.Dom.IElement role = bar.Find(".role-pick");
        Assert.Equal("Observer", role.TextContent);
        Assert.Equal(Role.Observer.Describe(), role.GetAttribute("title"));
    }

    [Fact]
    public void AHostAddsItsOwnLinksAfterTheViews()
    {
        Services.AddSingleton(new RoleContext(Role.Operator));

        IRenderedComponent<TopNav> bar = Render<TopNav>(parameters => parameters
            .AddChildContent("<a class=\"topnav-link\" href=\"/configure\">Configure</a>"));

        Assert.Equal(
            "/configure",
            bar.FindAll("a.topnav-link")[^1].GetAttribute("href"));
        Assert.Equal("also configures", bar.Find(".role-pick").GetAttribute("title"));
    }

    [Fact]
    public void EveryRoleIsDescribed()
    {
        Assert.All(
            Enum.GetValues<Role>(),
            role => Assert.False(string.IsNullOrEmpty(role.Describe()), $"{role}"));
    }
}
