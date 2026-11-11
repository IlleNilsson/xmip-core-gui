using System.Globalization;
using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Gui.Surface;

/// <summary>
/// How the Topology shows what moved (ADR-0052, amendment 2026-11-10). The
/// publisher states a link's rate and a drawn thing's activity, each per
/// second over its window (<c>observe::topology::window</c>); this says how
/// the canvas moves with them and what the inspector says of them, and
/// nothing else does. A rate is read in bands, one more for every doubling,
/// and the band drawn holds while the rate stays within it widened by a
/// fifth at each end, so a rate that wanders within its band, or a steady one
/// read a little each side of a band's edge from round to round, changes
/// nothing drawn and no running pulse starts over when the next round is
/// read. Nothing moved is band zero: no pulse runs, and the line or the node
/// is drawn still.
/// </summary>
public static class Motion
{
    /// <summary>The busiest band: more than 511 a second, the tenth
    /// doubling.</summary>
    public const int Busiest = 10;

    /// <summary>The most pulses any pattern runs on a line at once: a
    /// stream's four, and a retry's at the most. Each pulse leaves its share
    /// of this after the one before, so a pulse added or taken away leaves
    /// the others where they ride.</summary>
    public const int MostPulses = 4;

    // How far beyond its band a rate may wander before the band drawn moves:
    // a fifth of the band's edge, each side.
    private const double Margin = 0.2D;

    // The seconds a pulse takes along its line at the first band and at the
    // busiest; between them each band takes the same step off.
    private const double Slowest = 3.2D;

    private const double Fastest = 0.8D;

    /// <summary>The band of a rate per second: zero where nothing moved —
    /// zero, below it or not a number — else one up to one a second, one
    /// more for every doubling, and <see cref="Busiest"/> at the most.</summary>
    public static int Band(double perSecond)
    {
        return perSecond > 0D
            ? (int)Math.Clamp(Math.Ceiling(Math.Log2(1D + perSecond)), 1D, Busiest)
            : 0;
    }

    /// <summary>The band to draw a rate per second in, where the round before
    /// drew <paramref name="drawn"/>: the one drawn while the rate stays
    /// within it widened by a fifth at each end — a steady one a second read
    /// as 0.99 and 1.02 keeps its band — else the rate's own. Nothing moved
    /// is band zero at once, and anything that moves leaves band zero at
    /// once.</summary>
    public static int Band(double perSecond, int drawn)
    {
        int band = Band(perSecond);

        if (band == 0 || drawn is <= 0 or > Busiest || band == drawn)
        {
            return band;
        }

        double below = drawn == 1 ? 0D : (Math.Pow(2D, drawn - 1) - 1D) * (1D - Margin);
        double above = drawn == Busiest
            ? double.PositiveInfinity
            : (Math.Pow(2D, drawn) - 1D) * (1D + Margin);

        return perSecond > below && perSecond <= above ? drawn : band;
    }

    /// <summary>The seconds a pulse takes from a link's sender to its
    /// receiver at <paramref name="band"/>: 3.2 at the first, 0.8 at the
    /// busiest, so pulses leave more often as more moves.</summary>
    public static double Seconds(int band)
    {
        int step = Math.Clamp(band, 1, Busiest) - 1;
        double off = (Slowest - Fastest) / (Busiest - 1);

        return Slowest - (step * off);
    }

    /// <summary>How many pulses ride <paramref name="link"/> at once: none
    /// where nothing moved over it; else by its pattern — four for
    /// streaming, two for a request and its response or a session, one per
    /// attempt for a retry, up to <see cref="MostPulses"/>, and one for the
    /// rest.</summary>
    public static int Count(CommunicationLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return Band(link.Rate) == 0 ? 0 : link.Pattern switch
        {
            CommunicationPattern.Streaming => MostPulses,
            CommunicationPattern.Session or CommunicationPattern.RequestResponse => 2,
            CommunicationPattern.Retry => (int)Math.Clamp(link.Attempts, 1U, MostPulses),
            CommunicationPattern.SendReceive
                or CommunicationPattern.PublishConsume
                or CommunicationPattern.FireAndForget => 1,
            _ => 1,
        };
    }

    /// <summary>Whether pulse <paramref name="pulse"/> runs back, receiver to
    /// sender: every second one of a request and its response or of a
    /// session, the answer. Every other pulse runs sender to receiver.</summary>
    public static bool Back(CommunicationLink link, int pulse)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.Pattern is CommunicationPattern.RequestResponse or CommunicationPattern.Session
            && pulse % 2 == 1;
    }

    /// <summary>The radius a pulse is drawn at: six, outlined in the paper so
    /// it stands off the line it rides; a streaming link's five, since four
    /// ride its wider line.</summary>
    public static int Radius(CommunicationLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.Pattern == CommunicationPattern.Streaming ? 5 : 6;
    }

    /// <summary>How long a pulse takes at <paramref name="band"/>, as SVG
    /// writes a duration.</summary>
    public static string Duration(int band)
    {
        return Invariant(Seconds(band));
    }

    /// <summary>When pulse <paramref name="pulse"/> began at
    /// <paramref name="band"/>, as SVG writes it: before the canvas was
    /// drawn, by its share of <see cref="MostPulses"/> of the way, so a
    /// link's pulses ride it one after another, and one added — a retry's
    /// next attempt — moves none already riding.</summary>
    public static string Begin(int band, int pulse)
    {
        return pulse <= 0 ? Invariant(0D) : Invariant(-Seconds(band) * pulse / MostPulses);
    }

    /// <summary>Where still pulse <paramref name="pulse"/> of
    /// <paramref name="count"/> stands along its line, from zero at the
    /// sender to one at the receiver: each in the middle of its share, where
    /// movement is paused or reduced.</summary>
    public static double Along(int pulse, int count)
    {
        return (pulse + 0.5D) / Math.Max(count, 1);
    }

    /// <summary>What moved through a drawn thing, as the inspector says it
    /// beside Activity: the activity its publisher stated, per second, on
    /// the ladder every surface writes a rate on — a trickle never written as
    /// nothing — or idle where nothing moved, never a share of
    /// anything.</summary>
    public static string Said(double perSecond)
    {
        return Band(perSecond) == 0
            ? "idle: nothing moved"
            : $"{English.Rate(perSecond)} through it";
    }

    private static string Invariant(double seconds)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{seconds:0.##}s");
    }
}
