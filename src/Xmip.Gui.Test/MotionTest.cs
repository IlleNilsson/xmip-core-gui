using Xmip.Abi.Operate;
using Xmip.Gui.Surface;

namespace Xmip.Gui.Test;

/// <summary>
/// The one rule from what a publisher stated moved to how the Topology moves
/// (<see cref="Motion"/>; ADR-0052, amendment 2026-11-10): nothing moved is
/// still, more moves faster in bands, and a link's pattern keeps its pulses.
/// </summary>
public sealed class MotionTest
{
    [Fact]
    public void NothingMovedIsBandZeroAndEveryDoublingIsOneBandMoreUpToTheBusiest()
    {
        Assert.Equal(0, Motion.Band(0D));
        Assert.Equal(0, Motion.Band(-1D));
        Assert.Equal(0, Motion.Band(double.NaN));
        Assert.Equal(1, Motion.Band(0.08D));
        Assert.Equal(1, Motion.Band(1D));
        Assert.Equal(2, Motion.Band(1.5D));
        Assert.Equal(3, Motion.Band(6D));
        Assert.Equal(9, Motion.Band(511D));
        Assert.Equal(Motion.Busiest, Motion.Band(512D));
        Assert.Equal(Motion.Busiest, Motion.Band(1_000D));
        Assert.Equal(Motion.Busiest, Motion.Band(double.PositiveInfinity));

        double[] rates = [0.01D, 0.5D, 2D, 9D, 40D, 300D, 5_000D];
        int[] bands = [.. rates.Select(rate => Motion.Band(rate))];
        Assert.Equal(bands.Order(), bands);
    }

    [Fact]
    public void TheBandDrawnHoldsWhileTheRateStaysWithinAFifthOfItsEdges()
    {
        // One a second, measured over thirty seconds and a little more from
        // round to round: 0.99 and 1.02 sit each side of the first band's
        // edge, and the line keeps its pace.
        Assert.Equal(1, Motion.Band(0.99D));
        Assert.Equal(2, Motion.Band(1.02D));
        Assert.Equal(1, Motion.Band(1.02D, 1));
        Assert.Equal(2, Motion.Band(0.99D, 2));

        // Past the fifth, the band moves; nothing moved is still at once.
        Assert.Equal(2, Motion.Band(1.3D, 1));
        Assert.Equal(1, Motion.Band(0.7D, 2));
        Assert.Equal(0, Motion.Band(0D, 3));
        Assert.Equal(9, Motion.Band(400D, Motion.Busiest));
        Assert.Equal(Motion.Busiest, Motion.Band(450D, Motion.Busiest));

        // Drawn in none before, a rate is in its own band.
        Assert.All(
            (double[])[0.03D, 1.02D, 6D, 900D],
            rate => Assert.Equal(Motion.Band(rate), Motion.Band(rate, 0)));
    }

    [Fact]
    public void APulseTakesLessTimeTheBusierItsBand()
    {
        Assert.Equal(3.2D, Motion.Seconds(1), 6);
        Assert.Equal(0.8D, Motion.Seconds(Motion.Busiest), 6);
        double[] seconds = [.. Enumerable.Range(1, Motion.Busiest).Select(Motion.Seconds)];
        Assert.Equal(seconds.OrderDescending(), seconds);
    }

    [Fact]
    public void ALinkThatCarriedNothingHasNoPulseAndOneThatDidKeepsItsPatterns()
    {
        foreach (CommunicationPattern pattern in Enum.GetValues<CommunicationPattern>())
        {
            Assert.Equal(0, Motion.Count(Link(pattern, 0D, 3U)));
        }

        Assert.Equal(4, Motion.Count(Link(CommunicationPattern.Streaming, 2D, 0U)));
        Assert.Equal(2, Motion.Count(Link(CommunicationPattern.RequestResponse, 2D, 0U)));
        Assert.Equal(2, Motion.Count(Link(CommunicationPattern.Session, 2D, 0U)));
        Assert.Equal(1, Motion.Count(Link(CommunicationPattern.Retry, 2D, 0U)));
        Assert.Equal(3, Motion.Count(Link(CommunicationPattern.Retry, 2D, 3U)));
        Assert.Equal(4, Motion.Count(Link(CommunicationPattern.Retry, 2D, 9U)));
        Assert.Equal(1, Motion.Count(Link(CommunicationPattern.SendReceive, 2D, 0U)));

        CommunicationLink asked = Link(CommunicationPattern.RequestResponse, 2D, 0U);
        bool[] back = [Motion.Back(asked, 0), Motion.Back(asked, 1)];
        Assert.Equal([false, true], back);
        Assert.False(Motion.Back(Link(CommunicationPattern.Streaming, 2D, 0U), 1));
    }

    [Fact]
    public void ALinksPulsesLeaveAShareApartAndStandInTheMiddleOfTheirShares()
    {
        int band = Motion.Band(0.5D);

        // A quarter of the way apart, however many ride: a pulse added
        // leaves the others where they are.
        string[] begins = [.. Enumerable.Range(0, Motion.MostPulses).Select(
            pulse => Motion.Begin(band, pulse))];
        double[] along = [Motion.Along(0, 2), Motion.Along(1, 2)];

        Assert.Equal("3.2s", Motion.Duration(band));
        Assert.Equal(["0s", "-0.8s", "-1.6s", "-2.4s"], begins);
        Assert.Equal([0.25D, 0.75D], along);
    }

    [Fact]
    public void APulseIsLargeEnoughToStandOffTheLineItRides()
    {
        // The review of the first plan: r 6, outlined in the paper
        // (xmip.css); a stream's line is wider and carries four, at 5.
        Assert.Equal(6, Motion.Radius(Link(CommunicationPattern.SendReceive, 1D, 0U)));
        Assert.Equal(6, Motion.Radius(Link(CommunicationPattern.RequestResponse, 1D, 0U)));
        Assert.Equal(5, Motion.Radius(Link(CommunicationPattern.Streaming, 1D, 0U)));
    }

    [Fact]
    public void WhatMovedIsSaidPerSecondOrIdle()
    {
        Assert.Equal("idle: nothing moved", Motion.Said(0D));
        Assert.Equal("6/s through it", Motion.Said(6D));
        Assert.Equal("1.2K/s through it", Motion.Said(1_234D));

        // One Message in the publisher's half-minute window: a trickle,
        // never said as nothing.
        Assert.NotEqual("0/s through it", Motion.Said(0.033D));
        Assert.Equal("0.03/s through it", Motion.Said(0.033D));
    }

    private static CommunicationLink Link(
        CommunicationPattern pattern, double rate, uint attempts) =>
        new("link", "from", "to", pattern, TopologyOrigin.Both, "handoff", HealthState.Fine,
            rate > 0D ? 9UL : 0UL, rate, 0D, 0D, attempts, "said");
}
