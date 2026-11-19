// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App.Pictures;

/// <summary>
/// What the renderer asks the decoder to upscale to (<see cref="PictureScale"/>), from the chosen method and the size
/// the window shows the picture at, each frame. A new method or strength goes out at once; a new size only once it has held for
/// <see cref="SettleFrames"/> frames, so dragging the window's edge does not make the decoder's upscaler and ring again
/// at every step. A method the renderer runs itself asks for <see cref="PictureScale.None"/>. Render thread only.
/// </summary>
public sealed class ScaleRequest
{
    /// <summary>The frames a new size must hold before it is asked for (a third of a second at 60 Hz, less at 240).</summary>
    public const int SettleFrames = 20;

    private PictureScale _sent = PictureScale.None;
    private int _pendingWidth;
    private int _pendingHeight;
    private int _held;

    /// <summary>The request last handed out.</summary>
    public PictureScale Current => _sent;

    /// <summary>True, with the request, when this frame's method and size should go to the decoder (only a new request allocates).</summary>
    public bool Update(UpscaleMethod method, int strength, int width, int height, out PictureScale request)
    {
        request = _sent;

        if (!method.RunsOnDecoder())
        {
            return Send(PictureScale.None, ref request);
        }

        var chosen = method == _sent.Method && strength == _sent.Strength;

        if (chosen && width == _sent.Width && height == _sent.Height)
        {
            _held = 0;

            return false;
        }

        if (!chosen || Hold(width, height))
        {
            return Send(new PictureScale(method, width, height, strength), ref request);
        }

        return false;
    }

    // Whether the size has held long enough: the same size frame after frame.
    private bool Hold(int width, int height)
    {
        if (width != _pendingWidth || height != _pendingHeight)
        {
            (_pendingWidth, _pendingHeight, _held) = (width, height, 0);

            return false;
        }

        _held++;

        return _held >= SettleFrames;
    }

    private bool Send(PictureScale scale, ref PictureScale request)
    {
        if (ReferenceEquals(scale, _sent))
        {
            return false;
        }

        (_sent, _held) = (scale, 0);
        request = scale;

        return true;
    }
}
