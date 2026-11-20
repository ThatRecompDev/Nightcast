// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows.Tests.Recording;

/// <summary>Times the GPU work queued on a D3D11 device's immediate context between two marks, with timestamp queries.</summary>
public sealed unsafe class GpuStopwatch : IDisposable
{
    private readonly ID3D11DeviceContext _context;
    private nint _contextPointer;
    private nint _disjoint;
    private nint _start;
    private nint _end;

    public GpuStopwatch(StrategyBasedComWrappers wrappers, nint device)
    {
        var deviceApi = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        deviceApi.GetImmediateContext(out _contextPointer);

        Assert.True(deviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampDisjointQuery }, out _disjoint) >= 0);
        Assert.True(deviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampQuery }, out _start) >= 0);
        Assert.True(deviceApi.CreateQuery(new QueryDescription { Query = Direct3D11Values.TimestampQuery }, out _end) >= 0);
        ComObjects.Free(deviceApi);
        _context = ComObjects.Wrap<ID3D11DeviceContext>(wrappers, _contextPointer);
    }

    public void Begin()
    {
        _context.Begin(_disjoint);
        _context.End(_start);
    }

    /// <summary>Ends the bracket and waits for the GPU to reach it; its time in milliseconds, or null when the clock jumped.</summary>
    public double? End()
    {
        _context.End(_end);
        _context.End(_disjoint);
        TimestampDisjointData clock;
        ulong start;
        ulong end;

        while (_context.GetData(_disjoint, &clock, (uint)sizeof(TimestampDisjointData), 0) != 0)
        {
            Thread.Yield();
        }

        while (_context.GetData(_start, &start, sizeof(ulong), 0) != 0 || _context.GetData(_end, &end, sizeof(ulong), 0) != 0)
        {
            Thread.Yield();
        }

        if (clock.Disjoint != 0 || clock.Frequency == 0)
        {
            return null;
        }

        return (end - start) * 1000.0 / clock.Frequency;
    }

    public void Dispose()
    {
        ComObjects.Free(_context);
        ComObjects.Release(ref _contextPointer);
        ComObjects.Release(ref _end);
        ComObjects.Release(ref _start);
        ComObjects.Release(ref _disjoint);
    }
}
