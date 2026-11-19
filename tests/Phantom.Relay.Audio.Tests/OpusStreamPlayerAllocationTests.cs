// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.Audio.Tests.Fakes;

namespace Phantom.Relay.Audio.Tests;

/// <summary>Allocation budgets: a packet pushed and a device pull, steady or recovering losses, allocate nothing.</summary>
public sealed class OpusStreamPlayerAllocationTests
{
    [Fact]
    public void PushAndRenderAllocateNothing()
    {
        var simulation = StreamSimulation.Steady(1000);
        simulation.RunUntil(1000);
        var index = 101;

        var report = AllocationMeter.Measure(() =>
        {
            simulation.AdvanceTo(index * StreamSimulation.PacketMilliseconds);
            simulation.Send(index, false);
            simulation.Render();
            index++;
        });

        Assert.Equal(0, report.BytesPerRun);
    }

    [Fact]
    public void RecoveringLossesAllocatesNothing()
    {
        var simulation = StreamSimulation.Steady(1000);
        simulation.RunUntil(1000);
        var index = 101;

        // Every third packet is lost; every other one of those comes back through FEC, the rest are concealed.
        var report = AllocationMeter.Measure(() =>
        {
            simulation.AdvanceTo(index * StreamSimulation.PacketMilliseconds);

            if (index % 3 != 0)
            {
                simulation.Send(index, index % 2 == 0);
            }

            simulation.Render();
            index++;
        });

        Assert.Equal(0, report.BytesPerRun);
        Assert.True(simulation.Player.Stats.FecRecoveries > 0);
        Assert.True(simulation.Player.Stats.ConcealedFrames > 0);
    }

    [Fact]
    public void AnUnderrunAndTheRefillAllocateNothing()
    {
        var simulation = StreamSimulation.Steady(1000);
        simulation.RunUntil(1000);
        var index = 101;
        var step = 0;

        // Bursts of five packets after five pulls without any: concealment, the stop, the refill.
        var report = AllocationMeter.Measure(() =>
        {
            simulation.AdvanceTo((step * StreamSimulation.RenderMilliseconds) + 1010);

            if (step % 10 == 9)
            {
                for (var burst = 0; burst < 5; burst++)
                {
                    simulation.Send(index, false);
                    index++;
                }
            }

            simulation.Render();
            step++;
        });

        Assert.Equal(0, report.BytesPerRun);
        Assert.True(simulation.Player.Stats.Underruns > 0);
    }
}
