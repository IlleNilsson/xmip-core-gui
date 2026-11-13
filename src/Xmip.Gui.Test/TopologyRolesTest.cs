using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Abi.Operate;
using Xmip.Gui.Components;
using Xmip.Gui.Pages;
using Xmip.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The map focuses on roles (the owner, 2026-11-12: <i>We got to straighten
/// out the map. It has to focus on roles</i>; <i>Related by its role in the
/// cluster, not by name</i>; ADR-0052 and ADR-0056, amendments 2026-11-13):
/// every node stands where the runtime's layout puts it, scaled from the unit
/// square, and traffic moves none of them; a node says on hover what it is
/// for (<i>There has to be a tooltip, regardless of Web or MAUI. what are nodes
/// purpose</i>), on the Topology and on the Monitor; a line says how its ends
/// are related, and the legend names the relations. Web and Desktop render
/// these components alike.
/// </summary>
public sealed class TopologyRolesTest : BunitContext
{
    private static readonly TestCluster Names = TestCluster.Read();
    private static readonly string Receiver = Names.WithRole("receiving");
    private static readonly string Sender = Names.WithRole("sending");
    private static readonly string Operator = Names.WithRole("operational");
    private static readonly string Primary = Names.WithRole("storage");
    private static readonly string Standby = Names.Nodes.Last(node => Declared(node) == "storage");

    public TopologyRolesTest()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.xmip");
        Services.AddSingleton(ClusterSurfaces.Over(new SnapshotOperator(fixture)));
        Services.AddSingleton(new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()));
        Services.AddSingleton(new ProgramAudit("Xmip.Gui.Test", Unacted.Audit));
    }

    [Fact]
    public void ANodesTooltipSaysWhatItsRolesAreForAndAThingThatIsNoNodeSaysNone()
    {
        IRenderedComponent<TopologyGraph> canvas = Drawn(Mapped(busy: false));

        foreach (string node in (string[])[Receiver, Primary, Standby, Sender, Operator])
        {
            string title = Node(canvas, node).QuerySelector("title")!.TextContent;

            Assert.EndsWith(
                $"\n{English.Purpose(Declared(node))}", title, StringComparison.Ordinal);
        }

        // Operational and monitoring: a role a line.
        Assert.Equal(2, English.Purpose(Declared(Operator)).Split('\n').Length);

        TopologyNode stage = new(
            "node/x/receive", null, "receive", TopologyNodeKind.Stage,
            $"{Names.Scope}/node/x/receive", HealthState.Fine, TopologyOrigin.Both, 0D, 0D,
            "said");
        string staged = Node(Drawn(new TopologySnapshot(
            [stage], [], DateTimeOffset.UnixEpoch, "drawn for the test")), "receive")
            .QuerySelector("title")!.TextContent;
        Assert.DoesNotContain('\n', staged);
    }

    [Fact]
    public void EveryNodeStandsWhereTheRuntimeLaidItOutScaledAndTrafficMovesNone()
    {
        TopologySnapshot quiet = Mapped(busy: false);
        IRenderedComponent<TopologyGraph> canvas = Drawn(quiet);
        IReadOnlyList<TopologyPlace> places = quiet.PlacesAt("cluster");

        Assert.Equal(5, places.Count);
        foreach (TopologyPlace place in places)
        {
            string label = quiet.Nodes.Single(node => node.Id == place.Id).Label;

            Assert.Equal(Scaled(place.X, place.Y), Node(canvas, label).GetAttribute("transform"));
        }

        // By role: receiving left of Storage, the standby below its primary,
        // the operation band on top.
        Assert.True(Across(canvas, Receiver) < Across(canvas, Primary));
        Assert.True(Down(canvas, Primary) < Down(canvas, Standby));
        Assert.True(Down(canvas, Operator) < Down(canvas, Primary));

        // Traffic changes nothing: the same map drawn busy, every line used
        // and a line no relation explains observed beside them.
        IRenderedComponent<TopologyGraph> busy = Drawn(Mapped(busy: true));
        Assert.Equal(Transforms(canvas), Transforms(busy));
        Assert.Contains(
            $"{English.Name(TopologyRelation.CopiesTo)} · 48,213 · 310/s",
            busy.FindAll("g.topology-link .link-figure").Select(figure => figure.TextContent));
    }

    [Fact]
    public void AThingTheSnapshotGivesNoPlaceStandsInTheBottomRowInSnapshotOrder()
    {
        static TopologyNode Thing(string name) => new(
            $"node/{name}", "cluster", name, TopologyNodeKind.Node, $"{Names.Scope}/node/{name}",
            HealthState.Fine, TopologyOrigin.Both, 0D, 0D, "said", Declared(name));

        TopologySnapshot drawn = new(
            [
                new("cluster", null, Names.Name, TopologyNodeKind.Cluster, Names.Scope,
                    HealthState.Fine, TopologyOrigin.Both, 0D, 0D, "said"),
                Thing(Sender), Thing(Receiver), Thing(Operator),
            ],
            [],
            DateTimeOffset.UnixEpoch,
            "drawn for the test",
            Places: new Dictionary<string, IReadOnlyList<TopologyPlace>>
            {
                ["cluster"] =
                [
                    new($"node/{Receiver}", 0D, 1D, TopologyBand.Path, TopologyColumn.TxParty),
                    new($"node/{Operator}", 0.5D, 0.25D, TopologyBand.Operation,
                        TopologyColumn.Spanning),
                ],
            });
        IRenderedComponent<TopologyGraph> canvas = Drawn(drawn);

        Assert.Equal(Scaled(0D, 1D), Node(canvas, Receiver).GetAttribute("transform"));
        Assert.Equal(Scaled(0.5D, 0.25D), Node(canvas, Operator).GetAttribute("transform"));
        // The one thing laid out nowhere, centered in the frame's bottom row.
        Assert.Equal("translate(500 815)", Node(canvas, Sender).GetAttribute("transform"));
        Assert.Equal(
            [Sender, Receiver, Operator],
            canvas.FindAll("g.topology-node .node-label").Select(label => label.TextContent));
    }

    [Fact]
    public void TheLegendNamesEveryRelationButTrafficAndTheInspectorSaysEachLinks()
    {
        TopologySnapshot drawn = Mapped(busy: true);
        IRenderedComponent<TopologyGraph> canvas = Drawn(drawn);

        Assert.Equal(
            Enum.GetValues<TopologyRelation>()
                .Where(relation => relation != TopologyRelation.Traffic)
                .Select(English.Name),
            canvas.FindAll(".topology-legend span.relation").Select(span => span.TextContent));
        Assert.Contains("copies-to", Line(canvas, "copies").ClassList);

        IRenderedComponent<TopologyInspector> inspector = Render<TopologyInspector>(
            parameters => parameters
                .Add(shown => shown.Snapshot, drawn)
                .Add(shown => shown.FocusNode, drawn.Nodes.Single(node => node.Id == "cluster")));
        IElement copies = inspector.FindAll(".topology-traffic li").Single(item =>
            item.QuerySelector(".ends")!.TextContent == $"{Primary} → {Standby}");
        Assert.Equal(
            (English.Name(TopologyRelation.CopiesTo), "48,213 · 310/s",
                English.Name(TopologyOrigin.Both), $"copies {Primary}"),
            (copies.QuerySelector(".relation")!.TextContent,
                copies.QuerySelector(".figure")!.TextContent,
                copies.QuerySelector(".origin")!.TextContent,
                copies.QuerySelector(".evidence")!.TextContent));
    }

    [Fact]
    public void ANodesRowOnTheMonitorSaysOnHoverWhatTheNodeIsFor()
    {
        IRenderedComponent<Cluster> page = Render<Cluster>();
        IElement row = page.FindAll("section.nodes .row").Single(item =>
            item.QuerySelector("a.scope")!.TextContent.Trim() == Receiver);

        // The fixture's receiving node publishes that it declares receiving.
        Assert.Equal(English.Purpose("receiving"), row.GetAttribute("title"));
        Assert.All(
            page.FindAll("section.nodes .row"),
            item => Assert.False(string.IsNullOrEmpty(item.GetAttribute("title"))));
    }

    // The test cluster's map, read through the runtime as a surface reads a
    // publication: five nodes by their roles and four relations, quiet — every
    // line configured, nothing passed — or busy.
    private static TopologySnapshot Mapped(bool busy)
    {
        string origin = busy ? "both" : "configured";
        string text = $"node = \"{Names.Scope}\"\n[topology]\nsource = \"drawn for the test\"\n"
            + "[[topology.nodes]]\nid = \"cluster\"\nkind = \"cluster\"\n"
            + $"label = \"{Names.Name}\"\nscope = \"{Names.Scope}\"\n"
            + Thing(Receiver, null) + Thing(Primary, 0) + Thing(Standby, 1)
            + Thing(Sender, null) + Thing(Operator, null)
            + Link("writes", Receiver, Primary, TopologyRelation.WritesInto, origin, busy)
            + Link("hands", Primary, Sender, TopologyRelation.HandsWorkTo, origin, busy)
            + Link("copies", Primary, Standby, TopologyRelation.CopiesTo, origin, busy)
            + Link(
                "operates", Operator, Receiver, TopologyRelation.OperatesAndMonitors, origin, busy)
            + (busy
                ? Link("seen", Receiver, Sender, TopologyRelation.Traffic, "observed", busy)
                : string.Empty);

        return RuntimeLibrary.Rules.Publications.Read(text, out string refusal)?.Topology
            ?? throw new InvalidOperationException(refusal);
    }

    private static string Thing(string name, int? place) =>
        $"[[topology.nodes]]\nid = \"node/{name}\"\nparent = \"cluster\"\nlabel = \"{name}\"\n"
        + $"kind = \"node\"\nscope = \"{Names.Scope}/node/{name}\"\nroles = \"{Declared(name)}\"\n"
        + (place is { } at ? $"place = {at}\n" : string.Empty);

    private static string Link(
        string id, string from, string to, TopologyRelation relation, string origin, bool busy) =>
        $"[[topology.links]]\nid = \"{id}\"\nfrom = \"node/{from}\"\nto = \"node/{to}\"\n"
        + $"relation = \"{English.Word(relation)}\"\norigin = \"{origin}\"\n"
        + (busy ? "volume = 48213\nrate = 310.0\n" : string.Empty)
        + $"evidence = \"{id} {from}\"\n";

    // What the test cluster says a node declares.
    private static string Declared(string node) => Names.Roles[Names.Nodes.ToList().IndexOf(node)];

    // Where a place in the unit square stands on the canvas, as the canvas
    // scales it: across from 78 to 922, down from 110 to 760.
    private static string Scaled(double x, double y) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"translate({78D + (x * 844D):0.##} {110D + (y * 650D):0.##})");

    private IRenderedComponent<TopologyGraph> Drawn(TopologySnapshot drawing) =>
        Render<TopologyGraph>(parameters => parameters
            .Add(graph => graph.Snapshot, drawing)
            .Add(graph => graph.FocusId, "cluster"));

    private static IElement Node(IRenderedComponent<TopologyGraph> canvas, string label) =>
        canvas.FindAll("g.topology-node")
            .Single(node => node.QuerySelector(".node-label")!.TextContent == label);

    private static IElement Line(IRenderedComponent<TopologyGraph> canvas, string id) =>
        canvas.FindAll("g.topology-link")
            .Single(group => group.QuerySelector("path.link-line")!.Id!
                .StartsWith($"topology-{id}-", StringComparison.Ordinal));

    private static List<string?> Transforms(IRenderedComponent<TopologyGraph> canvas) =>
        [.. canvas.FindAll("g.topology-node").Select(node => node.GetAttribute("transform"))];

    private static double Across(IRenderedComponent<TopologyGraph> canvas, string label) =>
        Coordinate(canvas, label, 0);

    private static double Down(IRenderedComponent<TopologyGraph> canvas, string label) =>
        Coordinate(canvas, label, 1);

    private static double Coordinate(
        IRenderedComponent<TopologyGraph> canvas, string label, int which)
    {
        string inside = Node(canvas, label).GetAttribute("transform")!["translate(".Length..];

        return double.Parse(inside.TrimEnd(')').Split(' ')[which], CultureInfo.InvariantCulture);
    }
}
