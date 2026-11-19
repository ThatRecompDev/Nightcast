// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Core;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Media.Rendering;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App.Pictures;

/// <summary>
/// Draws the decoder's pictures where they were decoded, on a device that opens textures another device shares
/// (<see cref="GpuTextureFeatures.SharedTextures"/>) and says its adapter: no read-back and no upload. At
/// <see cref="Initialize"/> it offers the adapter to the source; it opens the source's ring once (again when a new ring
/// comes), and each frame takes the picture to show (<see cref="TryTake"/>), draws it inside its access, its padded
/// slot cropped to the picture (<see cref="Draw(Vector4, bool)"/>: into the window's pass, or the upscaler's target), and after the frame's commit ends the access and hands the read's
/// end back to the source (<see cref="EndFrame"/>). A device without the feature, or a ring it refuses, leaves the
/// pictures to the CPU path (the source is told so).
/// </summary>
public sealed class GpuPicturePresenter
{
    private const int NoRing = 0;

    private readonly IGraphicsDevice _device;
    private readonly IGpuPictureSource _source;
    private readonly Nv12PictureRenderer _renderer;
    private readonly Nv12PictureRenderer _offscreen;
    private GpuSharedTexture[] _imports = [];
    private GpuSharedFence _fence;
    private GpuPicture _current;
    private int _generation = NoRing;
    private bool _enabled;
    private bool _open;

    public GpuPicturePresenter(IGraphicsDevice device, IGpuPictureSource source)
    {
        _device = device;
        _source = source;
        _renderer = new Nv12PictureRenderer(device);
        _offscreen = new Nv12PictureRenderer(device);
    }

    /// <summary>Whether a picture came this way and is on screen.</summary>
    public bool HasPicture { get; private set; }

    /// <summary>The picture on screen.</summary>
    public GpuPicture Current => _current;

    /// <summary>Why the pictures do not come this way, when the device cannot take them; null when it can.</summary>
    public string? Refusal { get; private set; }

    /// <summary>Makes the renderer and offers the source the device's adapter, where the device can open shared textures.</summary>
    public void Initialize()
    {
        var capabilities = _device.Capabilities;

        if (!capabilities.Has(GpuTextureFeatures.SharedTextures) || capabilities.AdapterLuid == 0)
        {
            Refusal = $"the {capabilities.Backend} device opens no shared textures";

            return;
        }

        _renderer.Initialize(PassTarget.Frame, BlendMode.None);
        _offscreen.Initialize(PassTarget.Offscreen, BlendMode.None);
        _enabled = true;
        _source.Offer(capabilities.AdapterLuid);
    }

    /// <summary>Moves to the picture this refresh shows; true when a new one came.</summary>
    [HotPath]
    public bool TryTake()
    {
        if (!_enabled || !_source.TryTake(out var picture))
        {
            return false;
        }

        _current = picture;
        HasPicture = true;

        return true;
    }

    /// <summary>Draws the picture on screen into <paramref name="destination"/> of the window's pass, inside its access; false when its ring is not open.</summary>
    [HotPath]
    public bool Draw(Vector4 destination)
    {
        return Draw(destination, offscreen: false);
    }

    /// <summary>
    /// Draws the picture on screen into <paramref name="destination"/> of the open pass, inside its access: the window's,
    /// or an offscreen target's (<paramref name="offscreen"/>: the upscaler's); false when its ring is not open.
    /// </summary>
    [HotPath]
    public bool Draw(Vector4 destination, bool offscreen)
    {
        if (!HasPicture || !EnsureOpened())
        {
            return false;
        }

        var texture = _imports[_current.Slot];
        _device.BeginSharedAccess(texture, _fence, _current.ReadyValue);
        _open = true;

        var renderer = _renderer;

        if (offscreen)
        {
            renderer = _offscreen;
        }

        renderer.Colour = _current.Colour;
        renderer.Draw(texture, destination, Crop(texture));

        return true;
    }

    /// <summary>After the frame's commit: ends the access and tells the source what the read ends at.</summary>
    [HotPath]
    public void EndFrame()
    {
        if (!_open)
        {
            return;
        }

        var signal = _device.EndSharedAccess(_imports[_current.Slot]);
        _open = false;
        _source.Release(_current, signal.FenceHandle, signal.Value);
    }

    public void Cleanup()
    {
        EndFrame();
        Close();
        _renderer.Cleanup();
        _offscreen.Cleanup();
        _enabled = false;
    }

    // The part of the slot the picture covers: a decoder's surface may be padded past it (1080 coded as 1088).
    private Vector4 Crop(in GpuSharedTexture texture)
    {
        var right = Math.Min(1f, _current.Width / (float)texture.Width);
        var bottom = Math.Min(1f, _current.Height / (float)texture.Height);

        return new Vector4(0f, 0f, right, bottom);
    }

    // The ring the picture is in, opened once; a ring the device refuses ends the GPU path.
    private bool EnsureOpened()
    {
        if (_generation == _current.Generation)
        {
            return true;
        }

        var ring = _source.Ring;

        if (ring is null || ring.Generation != _current.Generation)
        {
            return false;
        }

        try
        {
            Open(ring);

            return true;
        }
        catch (InvalidOperationException refused)
        {
            Close();
            Refuse(refused.Message);

            return false;
        }
    }

    private void Open(GpuPictureRing ring)
    {
        Close();
        _imports = new GpuSharedTexture[ring.Slots.Length];

        for (var slot = 0; slot < ring.Slots.Length; slot++)
        {
            _imports[slot] = _device.ImportSharedTexture(ring.Slots[slot]);
        }

        _fence = _device.ImportSharedFence(ring.Fence);
        _generation = ring.Generation;
    }

    private void Close()
    {
        foreach (var import in _imports)
        {
            _device.Destroy(import);
        }

        _device.Destroy(_fence);
        _imports = [];
        _fence = default;
        _generation = NoRing;
    }

    private void Refuse(string reason)
    {
        Refusal = reason;
        HasPicture = false;
        _enabled = false;
        _source.Withdraw($"the renderer refused the shared textures: {reason}");
    }
}
