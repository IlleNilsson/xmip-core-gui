using AngleSharp.Dom;
using Bunit;
using Xmip.Abi.Operate;
using Xmip.Gui.Components;

namespace Xmip.Gui.Test;

/// <summary>
/// Motion follows traffic on the Topology (ADR-0052, amendment 2026-11-10):
/// a line carries pulses from its sender to its receiver only where
/// something moved over it, as often as its rate says, riding the drawn line;
/// a node pulses as fast as what moved through it and is still where nothing
/// did; paused, the same pulses stand along the line; and the inspector says
/// what moved in words. The owner, 2026-11-10: the board is to read as an
/// integration with pulses traveling between the nodes, where every live
/// node pulsed at full speed whatever it carried. Web and Desktop render
/// this one component.
/// </summary>
public sealed class TopologyMotionTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Processor = Names.WithRole("processing");
    private static readonly string Sender = Names.WithRole("sending");

    [Fact]
    public void ALineThatCarriedTrafficRunsPulsesFromItsSenderAndOneThatCarriedNoneIsStill()
    {
        IRenderedComponent<TopologyGraph> canvas = Drawn(Drawing(), paused: false);
        IElement used = Line(canvas, "used");
        IElement idle = Line(canvas, "idle");

        IElement[] riding =
            [.. used.QuerySelectorAll(".pulses-moving circle.flow-particle animateMotion mpath")];
        IElement line = used.QuerySelector("path.link-line")!;
        IElement pulse = Assert.Single(riding);
        Assert.Equal($"#{line.Id}", pulse.GetAttribute("href"));
        Assert.Null(pulse.ParentElement!.GetAttribute("keyPoints"));
        Assert.Empty(idle.QuerySelectorAll("circle.flow-particle"));

        // From its sender: the line it rides starts where the receiving node
        // stands.
        string from = NodeAt(canvas, Receiver);
        Assert.StartsWith($"M {from} Q ", line.GetAttribute("d"), StringComparison.Ordinal);
    }

    [Fact]
    public void ARequestsAnswerRunsBackAndAStreamCarriesFour()
    {
        IRenderedComponent<TopologyGraph> canvas = Drawn(Drawing(), paused: false);

        string?[] asked =
        [
            .. Line(canvas, "asked").QuerySelectorAll(".pulses-moving animateMotion")
                .Select(motion => motion.GetAttribute("keyPoints")),
        ];
        Assert.Equal([null, "1;0"], asked);
        Assert.Equal(
            4, Line(canvas, "streamed").QuerySelectorAll(".pulses-moving circle").Length);
    }

    [Fact]
    public void PulsesLeaveMoreOftenTheMoreMovesAndARoundInTheSameBandChangesNothing()
    {
        string Duration(double rate) =>
            Line(Drawn(Drawing(rate), paused: false), "used")
                .QuerySelector(".pulses-moving animateMotion")!
                .GetAttribute("dur")!;

        Assert.True(
            Seconds(Duration(100)) < Seconds(Duration(1)),
            $"{Duration(100)} against {Duration(1)}");
        // The next round read, its rate wandered within its band: the pulse
        // is not started over.
        Assert.Equal(Duration(1.5), Duration(1.6));
    }

    [Fact]
    public void ASteadyRateReadEachSideOfABandsEdgeKeepsItsPulsesAndItsRing()
    {
        // One a second over thirty seconds and a little more: 0.98 one
        // round, 1.02 the next, each side of the first band's edge; and a
        // node at about seven a second, 6.9 then 7.1, each side of the
        // third's. Neither a pulse nor a ring may start over.
        IRenderedComponent<TopologyGraph> canvas = Drawn(Drawing(0.98D, 6.9D), paused: false);
        (string?, string?) Timing() =>
            Line(canvas, "used").QuerySelector(".pulses-moving animateMotion") is { } motion
                ? (motion.GetAttribute("dur"), motion.GetAttribute("begin"))
                : (null, null);
        (string?, string?) before = Timing();
        string[] ring = [.. Node(canvas, Receiver).ClassList];

        canvas.Render(parameters => parameters.Add(graph => graph.Snapshot, Drawing(1.02D, 7.1D)));

        string[] after = [.. Node(canvas, Receiver).ClassList];
        Assert.Equal(before, Timing());
        Assert.Equal(ring, after);
        // Drawn afresh, each is in a band of its own.
        IRenderedComponent<TopologyGraph> fresh = Drawn(Drawing(1.02D, 7.1D), paused: false);
        Assert.NotEqual(
            before.Item1,
            Line(fresh, "used").QuerySelector(".pulses-moving animateMotion")!.GetAttribute("dur"));
        Assert.Contains("activity-4", Node(fresh, Receiver).ClassList);
        Assert.Contains("activity-3", ring);
    }

    [Fact]
    public void TheNextAttemptOfARetryAddsAPulseAndMovesNoneAlreadyRiding()
    {
        IRenderedComponent<TopologyGraph> canvas = Drawn(Drawing(retried: 2U), paused: false);
        string?[] Begins() =>
        [
            .. Line(canvas, "retried").QuerySelectorAll(".pulses-moving animateMotion")
                .Select(motion => motion.GetAttribute("begin")),
        ];
        string?[] two = Begins();

        canvas.Render(parameters => parameters.Add(graph => graph.Snapshot, Drawing(retried: 3U)));

        string?[] three = Begins();
        Assert.Equal(2, two.Length);
        Assert.Equal(3, three.Length);
        Assert.Equal(two, three[..2]);
    }

    [Fact]
    public void PausedThePulsesStandAlongTheLineWhereTheyRode()
    {
        IRenderedComponent<TopologyGraph> moving = Drawn(Drawing(), paused: false);
        IRenderedComponent<TopologyGraph> paused = Drawn(Drawing(), paused: true);

        Assert.Contains("paused", paused.Find("div.topology-canvas").ClassList);
        Assert.DoesNotContain("paused", moving.Find("div.topology-canvas").ClassList);
        Assert.Empty(paused.FindAll("animateMotion"));
        foreach (string id in (string[])["used", "asked", "streamed"])
        {
            IElement[] still =
                [.. Line(paused, id).QuerySelectorAll(".pulses-still circle.flow-particle")];
            Assert.Equal(
                Line(moving, id).QuerySelectorAll(".pulses-moving circle").Length, still.Length);
            Assert.All(
                still, pulse => Assert.False(string.IsNullOrEmpty(pulse.GetAttribute("cx"))));
        }

        // Moving, the still ones are drawn too, for a reader who asked for
        // reduced motion; the stylesheet decides which shows.
        Assert.Single(Line(moving, "used").QuerySelectorAll(".pulses-still circle"));
        Assert.Empty(Line(paused, "idle").QuerySelectorAll("circle"));
    }

    [Fact]
    public void ANodePulsesAsFastAsWhatMovedThroughItAndIsStillWhereNothingDid()
    {
        IRenderedComponent<TopologyGraph> canvas = Drawn(Drawing(), paused: false);

        string[] busy = [.. Node(canvas, Receiver).ClassList];
        string[] idle = [.. Node(canvas, Processor).ClassList];
        Assert.Contains("active", busy);
        Assert.Contains("activity-3", busy);
        Assert.DoesNotContain("active", idle);
        Assert.Contains("activity-0", idle);
    }

    [Fact]
    public void TheInspectorSaysWhatMovedThroughANodeInWordsNeverAShare()
    {
        TopologySnapshot drawing = Drawing();

        Assert.Equal("6/s through it", Activity(drawing, Receiver));
        Assert.Equal("idle: nothing moved", Activity(drawing, Processor));
    }

    // A cluster of the test cluster's three nodes, the receiving one busy
    // at `busy` and the processing one idle, and five lines: one used at
    // `rate`, one configured and never used, a request and its response, a
    // stream, and a retry on its `retried` attempt.
    private static TopologySnapshot Drawing(
        double rate = 1.5, double busy = 6D, uint retried = 1U)
    {
        TopologyNode Thing(string id, string? parent, string label, double activity) => new(
            id, parent, label, parent is null ? TopologyNodeKind.Cluster : TopologyNodeKind.Node,
            $"{Names.Scope}/{id}", HealthState.Fine, TopologyOrigin.Both, 0D, activity, "said");

        CommunicationLink Between(
            string id, string from, string to, CommunicationPattern pattern, ulong volume,
            double moving, uint attempts = 0U) => new(
            id, $"node/{from}", $"node/{to}", pattern,
            volume == 0 ? TopologyOrigin.Configured : TopologyOrigin.Both, "handoff",
            HealthState.Fine, volume, moving, 0D, 0D, attempts, "said");

        return new TopologySnapshot(
            [
                Thing("cluster", null, Names.Name, busy),
                Thing($"node/{Receiver}", "cluster", Receiver, busy),
                Thing($"node/{Processor}", "cluster", Processor, 0D),
                Thing($"node/{Sender}", "cluster", Sender, 1.5D),
            ],
            [
                Between("used", Receiver, Processor, CommunicationPattern.SendReceive, 6, rate),
                Between("idle", Processor, Sender, CommunicationPattern.SendReceive, 0, 0D),
                Between("asked", Receiver, Sender, CommunicationPattern.RequestResponse, 9, 3D),
                Between("streamed", Sender, Receiver, CommunicationPattern.Streaming, 90, 30D),
                Between(
                    "retried", Sender, Processor, CommunicationPattern.Retry, 4, 0.5D, retried),
            ],
            DateTimeOffset.UnixEpoch,
            "drawn for the test");
    }

    private IRenderedComponent<TopologyGraph> Drawn(TopologySnapshot drawing, bool paused) =>
        Render<TopologyGraph>(parameters => parameters
            .Add(graph => graph.Snapshot, drawing)
            .Add(graph => graph.FocusId, "cluster")
            .Add(graph => graph.MotionPaused, paused));

    // The group of the line drawn for the link `id`.
    private static IElement Line(IRenderedComponent<TopologyGraph> canvas, string id) =>
        canvas.FindAll("g.topology-link")
            .Single(group => group.QuerySelector("path.link-line")!.Id!
                .StartsWith($"topology-{id}-", StringComparison.Ordinal));

    private static IElement Node(IRenderedComponent<TopologyGraph> canvas, string label) =>
        canvas.FindAll("g.topology-node")
            .Single(node => node.QuerySelector(".node-label")!.TextContent == label);

    // Where the node labeled `label` stands, as the line from it begins.
    private static string NodeAt(IRenderedComponent<TopologyGraph> canvas, string label) =>
        Node(canvas, label).GetAttribute("transform")!
            .Replace("translate(", string.Empty, StringComparison.Ordinal)
            .TrimEnd(')');

    private static double Seconds(string duration) =>
        double.Parse(duration.TrimEnd('s'), System.Globalization.CultureInfo.InvariantCulture);

    private string Activity(TopologySnapshot drawing, string label)
    {
        TopologyNode focus = drawing.Nodes.Single(node => node.Label == label);
        IRenderedComponent<TopologyInspector> inspector = Render<TopologyInspector>(
            parameters => parameters
                .Add(shown => shown.Snapshot, drawing)
                .Add(shown => shown.FocusNode, focus));
        IElement term = inspector.FindAll(".topology-inspector dt")
            .Single(dt => dt.TextContent == "Activity");

        return term.NextElementSibling!.TextContent;
    }
}
