// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// Times GPU work between <see cref="Begin"/> and <see cref="End"/> with D3D11 timestamp queries in a disjoint
/// bracket. The result is read later (<see cref="TryRead"/>), once the GPU has reached the end mark.
/// </summary>
internal sealed unsafe class GpuTimer : IDisposable
{
    // Ticks per millisecond's ratio: the disjoint query reports ticks per second.
    private const double MillisecondsPerSecond = 1000;

    private readonly ComputeDevice _device;
    private nint _disjoint;
    private nint _start;
    private nint _end;

    public GpuTimer(ComputeDevice device)
    {
        _device = device;
        _ = device.DeviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampDisjointQuery }, out _disjoint);
        _ = device.DeviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampQuery }, out _start);
        _ = device.DeviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampQuery }, out _end);
    }

    public void Begin()
    {
        _device.ContextApi.Begin(_disjoint);
        _device.ContextApi.End(_start);
    }

    public void End()
    {
        _device.ContextApi.End(_end);
        _device.ContextApi.End(_disjoint);
    }

    /// <summary>The bracket's GPU time in milliseconds once ready; false while the GPU is still behind.</summary>
    public bool TryRead(out double milliseconds)
    {
        milliseconds = 0;
        TimestampDisjointData clock;
        ulong start;
        ulong end;

        if (_device.ContextApi.GetData(_disjoint, &clock, (uint)sizeof(TimestampDisjointData), 0) != 0)
        {
            return false;
        }

        if (_device.ContextApi.GetData(_start, &start, sizeof(ulong), 0) != 0 || _device.ContextApi.GetData(_end, &end, sizeof(ulong), 0) != 0)
        {
            return false;
        }

        if (clock.Disjoint != 0 || clock.Frequency == 0)
        {
            return false;
        }

        milliseconds = (end - start) * MillisecondsPerSecond / clock.Frequency;

        return true;
    }

    public void Dispose()
    {
        ComObjects.Release(ref _end);
        ComObjects.Release(ref _start);
        ComObjects.Release(ref _disjoint);
    }
}
