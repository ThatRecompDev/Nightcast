// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Audio.Tests.Fakes;

namespace Phantom.Relay.Audio.Tests;

/// <summary>The playout delay: its start, its growth on underruns and jitter, its slow shrink when steady.</summary>
public sealed class OpusStreamPlayerAdaptationTests
{
    [Fact]
    public void StartsAtTwoPackets()
    {
        var simulation = StreamSimulation.Steady(1000);

        simulation.RunUntil(1000);

        Assert.Equal(20, simulation.Player.Stats.TargetMilliseconds);
        Assert.InRange(simulation.Player.Stats.DelayMilliseconds, 18, 22);
        Assert.Equal(0, simulation.Player.Stats.Underruns);
    }

    [Fact]
    public void GrowsAtOnceOnAnUnderrunAndPlaysTheLatePackets()
    {
        var simulation = new StreamSimulation(800, Stall, _ => false);

        simulation.RunUntil(800);

        var stats = simulation.Player.Stats;
        Assert.Equal(1, stats.Underruns);
        Assert.True(stats.TargetMilliseconds > 20, $"target {stats.TargetMilliseconds} ms");
        Assert.Equal(0, stats.LateDrops);
        Assert.Contains(30, simulation.Decoder.Indexes(FakeDecodeKind.Packet));
        Assert.Contains(39, simulation.Decoder.Indexes(FakeDecodeKind.Packet));
    }

    [Fact]
    public void GrowsToCoverTheArrivalJitter()
    {
        var simulation = new StreamSimulation(3000, Jittered, _ => false);

        simulation.RunUntil(3000);

        var stats = simulation.Player.Stats;
        Assert.InRange(stats.TargetMilliseconds, 25, 30);
        Assert.Equal(0, stats.LateDrops);
    }

    [Fact]
    public void ShrinksSlowlyToTheLeastDelayWhenSteady()
    {
        var simulation = StreamSimulation.Steady(10000);

        simulation.RunUntil(1500);
        var held = simulation.Player.Stats.TargetMilliseconds;
        simulation.RunUntil(3000);
        var shrinking = simulation.Player.Stats.TargetMilliseconds;
        simulation.RunUntil(10000);

        Assert.Equal(20, held);
        Assert.InRange(shrinking, 11, 19);
        Assert.Equal(10, simulation.Player.Stats.TargetMilliseconds);
        Assert.InRange(simulation.Player.Stats.DelayMilliseconds, 9, 11);
        Assert.Equal(0, simulation.Player.Stats.Underruns);
    }

    /// <summary>Packets 30 to 39 are held up and come at once, 80 ms after packet 30 was due.</summary>
    private static double Stall(int index)
    {
        if (index >= 30 && index < 40)
        {
            return 380;
        }

        return index * StreamSimulation.PacketMilliseconds;
    }

    /// <summary>Each packet up to 24 ms late, spread over the range by a fixed pattern.</summary>
    private static double Jittered(int index)
    {
        return (index * StreamSimulation.PacketMilliseconds) + ((index * 7919) % 25);
    }
}
