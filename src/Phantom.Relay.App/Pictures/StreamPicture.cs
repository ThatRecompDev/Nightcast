// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Numerics;
using Phantom.Engine.Core;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Media.Rendering;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App.Pictures;

/// <summary>
/// The stream's picture on screen each frame, from either path: straight from the decoder's GPU
/// (<see cref="GpuPicturePresenter"/>) or read back and uploaded (<see cref="MovieRenderer"/>). Where the window shows it
/// larger than it came and an upscaler is chosen, it is drawn at its own size into a target and upscaled into the
/// letterboxed rectangle (<see cref="StreamUpscaler"/>, FSR 1 by default); else it is drawn into the rectangle as it is
/// (bilinear). Lossless Scaling's upscalers run where the pictures are decoded: the rectangle's size is asked of the
/// decoder (<see cref="ScaleRequest"/>, <see cref="IGpuPictureSource.Scale"/>), whose pictures then come at that size
/// and are drawn one to one; until then, or where they cannot run, FSR 1 upscales them here. The frame calls <see cref="Take"/>, then <see cref="Prepare"/> before the window's pass (the upscaler's
/// own passes, for a new picture only), <see cref="Draw"/> in it, and <see cref="EndFrame"/> after the commit. While the
/// device times its passes (<see cref="TimePasses"/>), the upscaler's passes before the window's are timed. Render thread only.
/// </summary>
public sealed class StreamPicture
{

    // A frame that upscaled a new picture has a pass of its own before the window's (two for FSR 1).
    private const int UpscaledPasses = 2;
    private static readonly Vector4 WholeTarget = new(0f, 0f, 1f, 1f);

    private readonly IGraphicsDevice _device;
    private readonly StreamConnection _stream;
    private readonly MovieRenderer _renderer;
    private readonly MovieRenderer _offscreen;
    private readonly GpuPicturePresenter? _gpu;
    private readonly StreamUpscaler _upscaler;
    private readonly ScaleRequest _request = new();
    private double[] _passTimes = [];
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

    /// <summary>FSR 1's RCAS sharpness in stops (0 sharpest).</summary>
    public float Sharpness => _upscaler.Sharpness;

    /// <summary>LS1's network asked of the decoder (<see cref="PictureScale.DefaultStrength"/> unless F7 changed it).</summary>
    public int Strength { get; private set; } = PictureScale.DefaultStrength;

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

    /// <summary>Once the device exists (its capabilities are readable only from here on): the renderers and the pass times.</summary>
    public void Initialize()
    {
        _passTimes = new double[Math.Max(1, _device.Capabilities.MaxTimedPasses)];
        _renderer.Initialize(PassTarget.Frame, BlendMode.None);
        _offscreen.Initialize(PassTarget.Offscreen, BlendMode.None);
        _upscaler.Initialize();
        _gpu?.Initialize();
    }

    /// <summary>Upscales with <paramref name="method"/> from the next frame on (a decoder-side one is asked of the decoder then).</summary>
    public void Use(UpscaleMethod method)
    {
        _upscaler.Use(method);
    }

    /// <summary>FSR 1's RCAS sharpens by <paramref name="sharpness"/> stops (0 sharpest) from the next frame on.</summary>
    public void UseSharpness(float sharpness)
    {
        _upscaler.UseSharpness(sharpness);
    }

    /// <summary>LS1 runs network <paramref name="strength"/> (0 to 4, more detail as it rises), asked of the decoder from the next frame on.</summary>
    public void UseStrength(int strength)
    {
        Strength = Math.Clamp(strength, PictureScale.MinimumStrength, PictureScale.MaximumStrength);
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
        Ask(destination, windowWidth, windowHeight);
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

    /// <summary>
    /// "FSR 1 (RCAS 0.2) → 3840x2160 0.30 ms", "upscale off", or the filter and why it waits (a window no larger than
    /// the picture). For a decoder-side method, what the decoder says of it (<paramref name="decoderUpscale"/>: "LS1 →
    /// 3600x2025 0.21 ms", or why it is off) and, while FSR 1 stands in for it here, FSR 1's part.
    /// </summary>
    public string Describe(string decoderUpscale)
    {
        var method = _upscaler.Method;

        if (method == UpscaleMethod.Bilinear)
        {
            return method.Label();
        }

        if (method.RunsOnDecoder())
        {
            return DescribeDecoder(method, decoderUpscale);
        }

        if (!_upscaled)
        {
            return $"{EngineLabel()}: the picture is not shown larger";
        }

        return Engine();
    }

    public void Cleanup()
    {
        TimePasses(false);
        _gpu?.Cleanup();
        _upscaler.Cleanup();
        _offscreen.Cleanup();
        _renderer.Cleanup();
    }

    // The decoder is asked for the rectangle's size with a decoder-side method (once it holds), for none with another.
    [HotPath]
    private void Ask(Vector4 destination, int windowWidth, int windowHeight)
    {
        var (width, height) = StreamUpscaler.RectangleSize(destination, windowWidth, windowHeight);

        if (_request.Update(_upscaler.Method, Strength, width, height, out var scale))
        {
            _stream.GpuPictures?.Scale(scale);
        }
    }

    private string DescribeDecoder(UpscaleMethod method, string decoderUpscale)
    {
        var state = decoderUpscale;

        if (state.Length == 0)
        {
            state = $"{method.Label()}: waiting for the decoder's GPU path";
        }

        if (!_upscaled)
        {
            return state;
        }

        return $"{state}; {Engine()}";
    }

    private string EngineLabel()
    {
        if (_upscaler.EngineMethod == UpscaleMethod.Xbr)
        {
            return UpscaleMethod.Xbr.Label();
        }

        return string.Create(CultureInfo.InvariantCulture, $"{UpscaleMethod.Fsr.Label()} (RCAS {_upscaler.Sharpness:F1})");
    }

    private string Engine()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{EngineLabel()} → {_upscaler.OutputWidth}x{_upscaler.OutputHeight} {_upscaleMilliseconds:F2} ms");
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
