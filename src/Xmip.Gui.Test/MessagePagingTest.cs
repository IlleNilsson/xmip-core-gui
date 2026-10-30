using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Abi.Operate;
using Xmip.Gui.Components;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The failed Messages at a Send Port, read through a surface the test
/// composes (<see cref="ScriptedSurface"/>): a page the node read empty with a
/// next place is followed until failures or the queue's end; a change of Port
/// starts again from the head; an answer that lists none is taken at its word
/// whatever the evidence names, and only a surface that cannot list falls back
/// to the evidence's Journey, and not once the evidence says none wait; and a
/// Dismiss is taken only once the Operator has confirmed it.
/// </summary>
public sealed class MessagePagingTest : BunitContext, IDisposable
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Node = $"{Names.Scope}/node/{Names.WithRole("sending")}";
    private static readonly string Port = Node + "/send/invoices";
    private static readonly string Other = Node + "/send/orders";

    private readonly string _place =
        Path.Combine(Path.GetTempPath(), $"xmip-gui-paging-{Guid.NewGuid():N}");

    private readonly ScriptedSurface _surface = new();

    public MessagePagingTest()
    {
        Directory.CreateDirectory(_place);
        Services.AddSingleton(new RoleContext(Role.Operator, "tester"));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", _place));
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_place, recursive: true);
        base.Dispose();
    }

    [Fact]
    public void AnEmptyPageWithANextPlaceIsFollowedToTheFailuresBeyondIt()
    {
        _surface.Listing = Pages(
            (0, [], 1_024),
            (1_024, [], 2_048),
            (2_048, [("j-late", 2_100)], null));

        IRenderedComponent<MessageActs> acts = At(Port, "failed in its queue 1");

        Assert.Equal(["j-late"], Listed(acts));
        Assert.Equal([0ul, 1_024ul, 2_048ul], _surface.Asked);
        Assert.Empty(acts.FindAll("button.more"));
    }

    [Fact]
    public void MoreFollowsTheNextPlaceAndAChangeOfPortStartsAgainAtTheHead()
    {
        _surface.Listing = Pages(
            (0, [("j-1", 3)], 5),
            (5, [], 9),
            (9, [("j-2", 12)], null));

        IRenderedComponent<MessageActs> acts = At(Port, "failed in its queue 2");
        Assert.Equal(["j-1"], Listed(acts));

        acts.Find("button.more").Click();
        Assert.Equal(["j-2"], Listed(acts));
        Assert.Equal([0ul, 5ul, 9ul], _surface.Asked);

        _surface.Asked.Clear();
        acts.Render(parameters => parameters.Add(view => view.Record, Record(Other, string.Empty)));
        Assert.Equal(0ul, _surface.Asked[0]);
        Assert.Equal(["j-1"], Listed(acts));
    }

    [Fact]
    public void AnAnswerThatListsNoneOffersNothingWhateverTheEvidenceNames()
    {
        _surface.Listing = Pages((0, [], null));

        IRenderedComponent<MessageActs> acts =
            At(Port, "failed in its queue 1; the Message of the Journey j-old failed: refused");

        Assert.Empty(acts.FindAll(".message-acts"));
    }

    [Fact]
    public void ASurfaceThatCannotListOffersTheEvidencesMessageOnlyWhileOneWaits()
    {
        _surface.Listing = null;

        Assert.Equal(
            ["j-old"],
            Listed(At(
                Port, "failed in its queue 1; the Message of the Journey j-old failed: refused")));
        Assert.Equal(
            ["j-old"],
            Listed(At(Port, "sent 1, failed 1; the Message of the Journey j-old failed: refused")));
        Assert.Empty(
            At(Port, "failed in its queue 0; the Message of the Journey j-old failed: refused")
                .FindAll(".message-acts"));
    }

    [Fact]
    public void StorageThatDoesNotAnswerIsSaidAndNothingIsOffered()
    {
        _surface.Listing = (scope, _) => MessageOperation.Read(
            scope, () => throw new InvalidOperationException("Xmip Storage is not open"));

        IRenderedComponent<MessageActs> acts =
            At(Port, "failed in its queue 1; the Message of the Journey j-old failed: refused");

        Assert.Equal(
            $"FAILED: the Messages that failed at {Port} could not be read: "
                + "Xmip Storage is not open",
            acts.Find(".message-unanswered").TextContent);
        Assert.Empty(Listed(acts));
        Assert.Empty(acts.FindAll("button"));
    }

    [Fact]
    public void ADismissNamesTheMessageAndIsTakenOnlyOnceConfirmed()
    {
        _surface.Listing = Pages((0, [("j-1", 3)], null));
        IRenderedComponent<MessageActs> acts = At(Port, "failed in its queue 1");

        acts.FindAll(".message-failed button")[1].Click();
        Assert.Empty(_surface.Acts);
        Assert.Equal(
            MessageActs.Asked("j-1"), acts.Find(".message-confirm .subs-act-said").TextContent);
        Assert.Contains("for good", MessageActs.Asked("j-1"), StringComparison.Ordinal);

        acts.Find("button.dismiss-keep").Click();
        Assert.Empty(acts.FindAll(".message-confirm"));
        Assert.Empty(_surface.Acts);

        acts.FindAll(".message-failed button")[1].Click();
        acts.Find("button.dismiss-confirm").Click();
        Assert.Equal(["dismiss j-1"], _surface.Acts);

        acts.FindAll(".message-failed button")[0].Click();
        Assert.Equal(["dismiss j-1", "retry j-1"], _surface.Acts);
    }

    private IRenderedComponent<MessageActs> At(string scope, string evidence)
    {
        return Render<MessageActs>(parameters => parameters
            .Add(view => view.Surface, _surface)
            .Add(view => view.Record, Record(scope, evidence)));
    }

    private static HealthRecord Record(string scope, string evidence)
    {
        return new HealthRecord(scope, HealthState.Fine, 0, evidence, DateTimeOffset.UnixEpoch);
    }

    private static List<string> Listed(IRenderedComponent<MessageActs> acts)
    {
        return
        [
            .. acts.FindAll(".message-failed").Select(row => row.GetAttribute("data-journey")!),
        ];
    }

    // Each place's page: the Messages found there and the next place.
    private static Func<string, ulong, FailedMessageList> Pages(
        params (ulong From, (string Journey, ulong Place)[] Found, ulong? Next)[] pages)
    {
        return (_, from) =>
        {
            (ulong _, (string Journey, ulong Place)[] found, ulong? next) =
                pages.Single(page => page.From == from);

            return new FailedMessageList(
                string.Empty,
                [
                    new FailedMessagePort(
                        Node,
                        "invoices",
                        (ulong)pages.Sum(page => page.Found.Length),
                        next,
                        [
                            .. found.Select(failed => new FailedMessageRecord(
                                Node, "invoices", failed.Journey, failed.Place, "refused")),
                        ]),
                ]);
        };
    }
}
