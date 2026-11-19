// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App.Tests.Pictures;

/// <summary>A source of GPU pictures the test fills by hand; it notes the offer, the withdrawal and every release.</summary>
public sealed class FakeGpuPictures : IGpuPictureSource
{
    private readonly Queue<GpuPicture> _pictures = new();

    public GpuPictureRing? Ring { get; set; }

    public GpuPictureReport Report { get; set; } = GpuPictureReport.Idle;

    public long OfferedLuid { get; private set; }

    public string? Withdrawn { get; private set; }

    public List<(GpuPicture Picture, nint Fence, ulong Value)> Releases { get; } = [];

    public void Offer(long adapterLuid)
    {
        OfferedLuid = adapterLuid;
    }

    public void Withdraw(string reason)
    {
        Withdrawn = reason;
    }

    public void Add(GpuPicture picture)
    {
        _pictures.Enqueue(picture);
    }

    public bool TryTake(out GpuPicture picture)
    {
        return _pictures.TryDequeue(out picture);
    }

    public void Release(in GpuPicture picture, nint fence, ulong value)
    {
        Releases.Add((picture, fence, value));
    }
}
