using System.Globalization;

namespace Xmip.Gui.Components;

// A line on the canvas: the curve it is drawn as between its two ends, a
// point along it — where a still pulse stands and its figure is written —
// and the element id its pulses ride by.
public partial class TopologyGraph
{
    // A line's element id, which its pulses ride by: the link's id with
    // every character no id may carry a dash, and the id's hash after it, so
    // two links whose ids differ only there — a Location named with a space
    // and one with a dash — never share a line.
    private static string SvgId(string id)
    {
        string hash = unchecked((uint)Stable(id)).ToString("x8", CultureInfo.InvariantCulture);

        return "topology-" + string.Concat(id.Select(character =>
            char.IsLetterOrDigit(character) ? character : '-')) + "-" + hash;
    }

    private static int Stable(string value) =>
        value.Aggregate(17, (hash, character) => unchecked((hash * 31) + character));

    // The curve's control point: bent off the straight line by a stable
    // amount per link, so two links between one pair do not overlap.
    private static (double X, double Y) Control(RenderedLink link)
    {
        double dx = link.To.X - link.From.X;
        double dy = link.To.Y - link.From.Y;
        double length = Math.Max(Math.Sqrt((dx * dx) + (dy * dy)), 1D);
        uint stable = unchecked((uint)Stable(link.Link.Id));
        double bend = ((stable % 5U) - 2D) * 14D;

        return (
            ((link.From.X + link.To.X) / 2D) - (dy / length * bend),
            ((link.From.Y + link.To.Y) / 2D) + (dx / length * bend));
    }

    private static string Curve(RenderedLink link)
    {
        (double controlX, double controlY) = Control(link);

        return FormattableString.Invariant(
                $"M {link.From.X:0.##} {link.From.Y:0.##} Q {controlX:0.##} {controlY:0.##} ")
            + FormattableString.Invariant($"{link.To.X:0.##} {link.To.Y:0.##}");
    }

    // Where the curve is at `along`, from zero at its sender to one at its
    // receiver: the quadratic curve's point, its two ends and its control
    // point weighed by how far along it is. A still pulse stands there, and
    // the figure written on the line just above the half.
    private static Spot Point(RenderedLink link, double along)
    {
        (double controlX, double controlY) = Control(link);
        double rest = 1D - along;

        return new Spot(
            (rest * rest * link.From.X) + (2D * rest * along * controlX)
                + (along * along * link.To.X),
            (rest * rest * link.From.Y) + (2D * rest * along * controlY)
                + (along * along * link.To.Y));
    }

    private static (string X, string Y) Middle(RenderedLink link)
    {
        Spot half = Point(link, 0.5D);

        return (Number(half.X), Number(half.Y - 6D));
    }

    private static string Number(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
