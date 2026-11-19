// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>
/// GPU timestamps at several points of one bracket (one disjoint query), read back by waiting: the time from the first
/// mark to each of the others, in milliseconds. For measurements, not for the stream.
/// </summary>
internal sealed unsafe class GpuMarks : IDisposable
{
    private readonly ComputeDevice _device;
    private readonly nint[] _marks;
    private nint _disjoint;

    public GpuMarks(ComputeDevice device, int marks)
    {
        _device = device;
        _marks = new nint[marks];
        _ = device.DeviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampDisjointQuery }, out _disjoint);

        for (var index = 0; index < marks; index++)
        {
            _ = device.DeviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampQuery }, out _marks[index]);
        }
    }

    public void Begin()
    {
        _device.ContextApi.Begin(_disjoint);
        Mark(0);
    }

    public void Mark(int index)
    {
        _device.ContextApi.End(_marks[index]);
    }

    public void End()
    {
        _device.ContextApi.End(_disjoint);
    }

    /// <summary>Waits for the GPU and gives each mark's time after the first; false when the clock was disjoint.</summary>
    public bool Read(double[] milliseconds)
    {
        TimestampDisjointData clock;
        var started = Stopwatch.StartNew();

        while (_device.ContextApi.GetData(_disjoint, &clock, (uint)sizeof(TimestampDisjointData), 0) != 0)
        {
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(5), "the GPU never reached the end of the marks");
            Thread.Yield();
        }

        var first = Value(0);

        for (var index = 1; index < _marks.Length; index++)
        {
            milliseconds[index] = (Value(index) - first) * 1000.0 / clock.Frequency;
        }

        return clock.Disjoint == 0;
    }

    public void Dispose()
    {
        for (var index = 0; index < _marks.Length; index++)
        {
            ComObjects.Release(ref _marks[index]);
        }

        ComObjects.Release(ref _disjoint);
    }

    private ulong Value(int index)
    {
        ulong value;

        while (_device.ContextApi.GetData(_marks[index], &value, sizeof(ulong), 0) != 0)
        {
            Thread.Yield();
        }

        return value;
    }
}
