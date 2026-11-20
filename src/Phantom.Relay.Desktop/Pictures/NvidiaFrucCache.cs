// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Nvidia;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// The decoder's one NVIDIA FRUC: FRUC can be made only once per D3D11 device and only one may live in the process
/// (<see cref="FrucGenerator"/>), so it is made the first time it is asked for on a device and kept, paused, while
/// frame generation is off or runs LSFG, until the device or the picture size changes or the decoder goes. Why it
/// cannot be made is kept too, so it is tried and logged once per device. Decode thread only.
/// </summary>
internal sealed class NvidiaFrucCache : IDisposable
{
    private readonly ILogger _log;
    private FrucGenerator? _generator;
    private nint _device;
    private int _width;
    private int _height;
    private string? _refusal;
    private string? _folder;
    private bool _searched;

    public NvidiaFrucCache(ILogger log)
    {
        _log = log;
    }

    /// <summary>The device's FRUC for pictures of <paramref name="width"/> x <paramref name="height"/>; null and why when it cannot be had.</summary>
    public FrucGenerator? TryGet(nint device, int width, int height, out string? problem)
    {
        if (device != _device || width != _width || height != _height)
        {
            Replace(device, width, height);
        }

        problem = _refusal;

        return _generator;
    }

    public void Dispose()
    {
        _generator?.Dispose();
        _generator = null;
        _device = 0;
    }

    // Another device or size: the old FRUC goes before the new one is made (one per process).
    private void Replace(nint device, int width, int height)
    {
        var sameDevice = device == _device;
        Dispose();
        (_device, _width, _height) = (device, width, height);

        if (sameDevice && _refusal is null)
        {
            // FRUC was made on this device before: a second one would not share its fence.
            _refusal = "NVIDIA FRUC was already made once on the decoder's GPU device (the picture size changed); it comes back with a new stream";

            return;
        }

        _refusal = null;
        _generator = FrucGenerator.TryCreate(Folder(), device, width, height, FrucSurfaceFormat.Rgba, _log, out _refusal);
    }

    private string? Folder()
    {
        if (!_searched)
        {
            _folder = NvidiaSdkFolder.Find();
            _searched = true;
        }

        return _folder;
    }
}
