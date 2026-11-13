using Xmip.Abi.Operate;

namespace Xmip.Gui.Components;

// Where each drawn thing stands on the canvas: where the runtime laid it out
// beneath the open node (TopologySnapshot.PlacesAt; observe::topology::layout,
// the one rule, by role and the order of [storage] nodes), scaled from the
// unit square into the view box. Nothing here works a place out: until
// 2026-11-13 the canvas set nodes in a ring, or in columns by how few links
// from where the streams enter reached each, sorted by label (the owner,
// 2026-11-12: "We got to straighten out the map. It has to focus on roles").
public partial class TopologyGraph
{
    // The unit square on the canvas. Across, from the TX-Parties, which send
    // into the cluster, at the left edge, to the RX-Parties, which it delivers
    // to, at the right, each outside the open node's frame; down, from below
    // the frame's label to above the row of things laid out nowhere. The
    // layout keeps a line about 0.05 of the square from every node it does
    // not join, which down this span is more than half a node's height (60),
    // so a line clears the nodes it passes.
    private const double Left = 78D;
    private const double Right = 922D;
    private const double Top = 110D;
    private const double Bottom = 760D;

    // Where a visible thing the layout gave no place stands: in a row along
    // the bottom of the frame, in the order the snapshot holds them —
    // deterministic, and never by a label.
    private const double Unplaced = 815D;

    private IReadOnlyList<PlacedNode> PlacedNodes
    {
        get
        {
            IReadOnlyList<TopologyNode> visible = VisibleNodes;
            Dictionary<string, TopologyPlace> places = Snapshot.PlacesAt(Focus?.Id)
                .DistinctBy(place => place.Id)
                .ToDictionary(place => place.Id, StringComparer.Ordinal);
            int unplaced = visible.Count(node => !places.ContainsKey(node.Id));
            int next = 0;
            List<PlacedNode> placed = [];

            foreach (TopologyNode node in visible)
            {
                placed.Add(places.TryGetValue(node.Id, out TopologyPlace? place)
                    ? Scaled(node, place)
                    : Fallback(node, next++, unplaced));
            }

            return placed;
        }
    }

    // A place in the unit square, x left to right and y top to bottom, on the
    // canvas.
    private static PlacedNode Scaled(TopologyNode node, TopologyPlace place)
    {
        return new PlacedNode(
            node,
            Left + (Math.Clamp(place.X, 0D, 1D) * (Right - Left)),
            Top + (Math.Clamp(place.Y, 0D, 1D) * (Bottom - Top)));
    }

    // The `index`th of `count` things laid out nowhere, spread across the
    // frame's bottom row.
    private PlacedNode Fallback(TopologyNode node, int index, int count)
    {
        return new PlacedNode(
            node, Frame.Left + (Frame.Width * (index + 0.5D) / count), Unplaced);
    }
}
