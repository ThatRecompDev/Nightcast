// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Audio.Tests.Fakes;

namespace Phantom.Relay.Audio.Tests;

/// <summary>Reordering, loss with and without FEC, and late packets, on a stream that crosses the sequence wrap.</summary>
public sealed class OpusStreamPlayerLossTests
{
    [Fact]
    public void PlaysSwappedPacketsInSequenceOrder()
    {
        var simulation = new StreamSimulation(500, Swapped, _ => false);

        simulation.RunUntil(500);

        var played = simulation.Decoder.Indexes(FakeDecodeKind.Packet);
        Assert.Equal(Enumerable.Range(played[0], played.Count), played);
        Assert.Contains(20, played);
        Assert.Contains(21, played);
        Assert.Empty(simulation.Decoder.Indexes(FakeDecodeKind.Lost));
        Assert.Equal(0, simulation.Player.Stats.LateDrops);
    }

    [Fact]
    public void RebuildsALostPacketFromTheNextPacketsFec()
    {
        var simulation = new StreamSimulation(500, LoseTwenty, _ => true);

        simulation.RunUntil(500);

        var calls = simulation.Decoder.Calls.ToList();
        var rebuilt = calls.IndexOf(new FakeDecode(FakeDecodeKind.Fec, 20));
        Assert.Equal(new FakeDecode(FakeDecodeKind.Packet, 19), calls[rebuilt - 1]);
        Assert.Equal(new FakeDecode(FakeDecodeKind.Packet, 21), calls[rebuilt + 1]);
        Assert.Equal(1, simulation.Player.Stats.FecRecoveries);
        Assert.Equal(0, simulation.Player.Stats.ConcealedFrames);
    }

    [Fact]
    public void ConcealsALostPacketWhenTheNextCarriesNoFec()
    {
        var simulation = new StreamSimulation(500, LoseTwenty, _ => false);

        simulation.RunUntil(500);

        var calls = simulation.Decoder.Calls.ToList();
        var concealed = calls.IndexOf(new FakeDecode(FakeDecodeKind.Lost, -1));
        Assert.Equal(new FakeDecode(FakeDecodeKind.Packet, 19), calls[concealed - 1]);
        Assert.Equal(new FakeDecode(FakeDecodeKind.Packet, 21), calls[concealed + 1]);

        // Concealed once, as a loss: the stream itself was on time.
        Assert.Equal(1, simulation.Player.Stats.ConcealedFrames);
        Assert.Equal(0, simulation.Player.Stats.FecRecoveries);
        Assert.Equal(0, simulation.Player.Stats.Underruns);
    }

    [Fact]
    public void DropsAPacketThatArrivesAfterItsTime()
    {
        var simulation = new StreamSimulation(600, DelayTwenty, _ => false);

        simulation.RunUntil(600);

        Assert.DoesNotContain(20, simulation.Decoder.Indexes(FakeDecodeKind.Packet));
        Assert.Contains(21, simulation.Decoder.Indexes(FakeDecodeKind.Packet));
        Assert.Equal(1, simulation.Player.Stats.LateDrops);
        Assert.Equal(1, simulation.Player.Stats.ConcealedFrames);
    }

    [Fact]
    public void RendersTheDecodedSoundOnceSteady()
    {
        var simulation = StreamSimulation.Steady(300);

        simulation.RunUntil(300);

        var played = simulation.Decoder.Indexes(FakeDecodeKind.Packet);
        var lowest = FakeOpusDecoder.Level(played[^4]);
        var highest = FakeOpusDecoder.Level(played[^1]);
        Assert.All(simulation.Output, sample => Assert.InRange(sample, lowest, highest));
        Assert.True(simulation.Player.Stats.Playing);
    }

    private static double Swapped(int index)
    {
        if (index == 20)
        {
            return 205;
        }

        if (index == 21)
        {
            return 200;
        }

        return index * StreamSimulation.PacketMilliseconds;
    }

    private static double LoseTwenty(int index)
    {
        if (index == 20)
        {
            return double.NaN;
        }

        return index * StreamSimulation.PacketMilliseconds;
    }

    private static double DelayTwenty(int index)
    {
        if (index == 20)
        {
            return 350;
        }

        return index * StreamSimulation.PacketMilliseconds;
    }
}
