// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Numerics;
using Phantom.Engine.Core;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Media.Rendering;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream;

namespace Phantom.Relay.App.Pictures;

/// <summary>
/// The stream's picture on screen each frame, from either path: straight from the decoder's GPU
/// (<see cref="GpuPicturePresenter"/>) or read back and uploaded (<see cref="MovieRenderer"/>). Where the window shows it
/// larger than it came and an upscaler is chosen, it is drawn at its own size into a target and upscaled into the
/// letterboxed rectangle (<see cref="StreamUpscaler"/>, FSR 1 by default); else it is drawn into the rectangle as it is
/// (bilinear). The frame calls <see cref="Take"/>, then <see cref="Prepare"/> before the window's pass (the upscaler's
/// own passes, for a new picture only), <see cref="Draw"/> in it, and <see cref="EndFrame"/> after the commit. While the
/// device times its passes (<see cref="TimePasses"/>), the upscaler's passes before the window's are timed. Render thread only.
/// </summary>
public sealed class StreamPicture
{
    private const string OffLabel = "upscale off";
    private const string FsrLabel = "FSR 1";
    private const string XbrLabel = "xBR";

    // A frame that upscaled a new picture has a pass of its own before the window's (two for FSR 1).
    private const int UpscaledPasses = 2;
    private static readonly Vector4 WholeTarget = new(0f, 0f, 1f, 1f);

    private readonly IGraphicsDevice _device;
    private readonly StreamConnection _stream;
    private readonly MovieRenderer _renderer;
    private readonly MovieRenderer _offscreen;
    private readonly GpuPicturePresenter? _gpu;
    private readonly StreamUpscaler _upscaler;
    private readonly double[] _passTimes;
    private bool _upscaled;
    private bool _timing;
    private double _upscaleMilliseconds;

    public StreamPicture(IGraphicsDevice device, StreamConnection stream)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(stream);

        _device = device;
        _stream = stream;
        _renderer = new MovieRenderer(device);
        _offscreen = new MovieRenderer(device);
        _upscaler = new StreamUpscaler(device);
        _passTimes = new double[Math.Max(1, device.Capabilities.MaxTimedPasses)];

        if (stream.GpuPictures is not null)
        {
            _gpu = new GpuPicturePresenter(device, stream.GpuPictures);
        }
    }

    /// <summary>Whether the frame took a new picture.</summary>
    public bool Fresh { get; private set; }

    /// <summary>Whether the picture on screen came on the GPU path.</summary>
    public bool OnGpu { get; private set; }

    /// <summary>Why the device takes no pictures on the GPU; null when it does (or there is no GPU path).</summary>
    public string? Refusal => _gpu?.Refusal;

    /// <summary>The upscaler chosen (<see cref="UpscaleMethod.Bilinear"/>: off).</summary>
    public UpscaleMethod Upscaler => _upscaler.Method;

    /// <summary>When the picture on screen was ready, on the stream's clock.</summary>
    public long ReadyAt
    {
        get
        {
            if (OnGpu && _gpu is not null)
            {
                return _gpu.Current.DecodedAt;
            }

            return _stream.Pictures.FrontReadyAt;
        }
    }

    public void Initialize()
    {
        _renderer.Initialize(PassTarget.Frame, BlendMode.None);
        _offscreen.Initialize(PassTarget.Offscreen, BlendMode.None);
        _upscaler.Initialize();
        _gpu?.Initialize();
    }

    /// <summary>Upscales with <paramref name="method"/> from the next frame on.</summary>
    public void Use(UpscaleMethod method)
    {
        _upscaler.Use(method);
    }

    /// <summary>Times the device's passes (where it can), so <see cref="Describe"/> says what the upscaler's cost.</summary>
    public void TimePasses(bool on)
    {
        if (on == _timing || !_device.Capabilities.Has(GpuFeatures.FrameTimes))
        {
            return;
        }

        _device.TimePasses(on);
        _timing = on;
    }

    /// <summary>A new picture from either path; the GPU path's wins when both have one (the switch to it).</summary>
    [HotPath]
    public void Take()
    {
        Fresh = false;

        if (_stream.Pictures.TryTake())
        {
            OnGpu = false;
            Fresh = true;
        }

        if (_gpu is not null && _gpu.TryTake())
        {
            OnGpu = true;
            Fresh = true;
        }

        ReadPassTimes();
    }

    /// <summary>Before the window's pass: whether this frame upscales into <paramref name="destination"/>, and for a new picture its passes.</summary>
    [HotPath]
    public void Prepare(Vector4 destination, int windowWidth, int windowHeight)
    {
        var (width, height) = PictureSize();
        _upscaled = _upscaler.Plan(width, height, destination, windowWidth, windowHeight);

        if (!_upscaled || (!Fresh && !_upscaler.IsStale))
        {
            return;
        }

        _upscaler.BeginPicture();
        DrawPicture(WholeTarget, offscreen: true);
        _upscaler.EndPicture();
    }

    /// <summary>In the window's pass: the picture into <paramref name="destination"/>, upscaled when <see cref="Prepare"/> said so.</summary>
    [HotPath]
    public void Draw(Vector4 destination)
    {
        if (_upscaled)
        {
            _upscaler.Draw();

            return;
        }

        DrawPicture(destination, offscreen: false);
    }

    /// <summary>After the frame's commit: the GPU path's access ends.</summary>
    [HotPath]
    public void EndFrame()
    {
        _gpu?.EndFrame();
    }

    /// <summary>"FSR 1 → 3840x2160 0.3 ms", "upscale off", or the filter and why it waits (a window no larger than the picture).</summary>
    public string Describe()
    {
        if (_upscaler.Method == UpscaleMethod.Bilinear)
        {
            return OffLabel;
        }

        var name = FsrLabel;

        if (_upscaler.Method == UpscaleMethod.Xbr)
        {
            name = XbrLabel;
        }

        if (!_upscaled)
        {
            return $"{name}: the picture is not shown larger";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{name} → {_upscaler.OutputWidth}x{_upscaler.OutputHeight} {_upscaleMilliseconds:F2} ms");
    }

    public void Cleanup()
    {
        TimePasses(false);
        _gpu?.Cleanup();
        _upscaler.Cleanup();
        _offscreen.Cleanup();
        _renderer.Cleanup();
    }

    private (int Width, int Height) PictureSize()
    {
        if (OnGpu && _gpu is not null && _gpu.HasPicture)
        {
            return (_gpu.Current.Width, _gpu.Current.Height);
        }

        if (_stream.Pictures.HasPicture)
        {
            return (_stream.Pictures.Front.Width, _stream.Pictures.Front.Height);
        }

        return (0, 0);
    }

    [HotPath]
    private void DrawPicture(Vector4 destination, bool offscreen)
    {
        if (OnGpu && _gpu is not null)
        {
            _gpu.Draw(destination, offscreen);

            return;
        }

        if (!_stream.Pictures.HasPicture)
        {
            return;
        }

        var renderer = _renderer;

        if (offscreen)
        {
            renderer = _offscreen;
        }

        renderer.Upload(_stream.Pictures.Front);
        renderer.Draw(destination);
    }

    // A timed frame that upscaled a new picture: its passes before the window's (the picture's and EASU's).
    [HotPath]
    private void ReadPassTimes()
    {
        if (!_timing || !_device.TryTakePassTimes(out _, _passTimes, out var passes) || passes < UpscaledPasses)
        {
            return;
        }

        var total = 0.0;

        for (var index = 0; index < passes - 1 && index < _passTimes.Length; index++)
        {
            total += _passTimes[index];
        }

        _upscaleMilliseconds = total;
    }
}
