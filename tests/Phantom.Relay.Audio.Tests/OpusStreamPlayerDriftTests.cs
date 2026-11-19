// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Audio.Tests.Fakes;

namespace Phantom.Relay.Audio.Tests;

/// <summary>The console's clock and the sound card's disagree; the resampling ratio holds the delay over two minutes.</summary>
public sealed class OpusStreamPlayerDriftTests
{
    [Theory]
    [InlineData(1.003)]
    [InlineData(0.997)]
    [InlineData(1.0001)]
    public void HoldsTheDelayAgainstClockDrift(double consoleSpeed)
    {
        var simulation = new StreamSimulation(120000, index => index * StreamSimulation.PacketMilliseconds / consoleSpeed, _ => false);

        simulation.RunUntil(120000);

        var stats = simulation.Player.Stats;
        Assert.InRange(stats.DriftRatio, consoleSpeed - 0.0003, consoleSpeed + 0.0003);
        Assert.InRange(stats.DelayMilliseconds, stats.TargetMilliseconds - 1, stats.TargetMilliseconds + 1);
        Assert.Equal(10, stats.TargetMilliseconds);

        // Nothing went wrong on the way.
        Assert.Equal(0, stats.Underruns);
        Assert.Equal(0, stats.LateDrops);
        Assert.Equal(0, stats.SkippedPackets);
    }

    [Fact]
    public void ClampsTheRatioWhenTheDriftIsBeyondHalfAPercent()
    {
        var simulation = new StreamSimulation(20000, index => index * StreamSimulation.PacketMilliseconds / 1.01, _ => false);

        simulation.RunUntil(20000);

        var stats = simulation.Player.Stats;
        Assert.Equal(1.005, stats.DriftRatio, 9);
        Assert.True(stats.SkippedPackets > 0, "a backlog the ratio cannot drain is cut");
    }
}
