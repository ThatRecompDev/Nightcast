// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Numerics;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Media.Rendering;
using Phantom.Engine.Media.Video;
using Phantom.Relay.App.Pictures;

namespace Phantom.Relay.App.Tests.Pictures;

/// <summary>
/// A hidden window's frames for <see cref="StreamUpscalerLiveTests"/>: first FSR 1 from 2560 x 1440 to 3840 x 2160
/// (the picture's pass, EASU, RCAS into a target of the window's format), then the picture drawn bilinearly into that
/// target, each pass timed by the device; the window closes when both are measured.
/// </summary>
internal sealed class UpscaleBench : IApplication
{
    private const int PictureWidth = 2560;
    private const int PictureHeight = 1440;
    private const int OutputWidth = 3840;
    private const int OutputHeight = 2160;
    private const int WarmUp = 30;
    private const int Measured = 120;

    private static readonly Vector4 Whole = new(0f, 0f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private readonly IGraphicsDevice _device;
    private readonly IWindow _window;
    private readonly StreamUpscaler _upscaler;
    private readonly MovieRenderer _offscreen;
    private readonly MovieRenderer _frame;
    private readonly VideoFrame _picture = new();
    private readonly double[] _times = new double[8];
    private readonly double[] _fsr = new double[3];
    private readonly double[] _bilinear = new double[1];
    private GpuRenderTarget _target;
    private int _frames;
    private int _fsrCount;
    private int _bilinearCount;

    public UpscaleBench(IGraphicsDevice device, IWindow window)
    {
        _device = device;
        _window = window;
        _upscaler = new StreamUpscaler(device);
        _offscreen = new MovieRenderer(device);
        _frame = new MovieRenderer(device);
    }

    public string? Problem { get; private set; }

    /// <summary>Managed bytes the measured FSR frames' upscaling allocated.</summary>
    public long Allocated { get; private set; }

    public void Initialize()
    {
        if (!_device.Capabilities.Has(GpuFeatures.FrameTimes))
        {
            Problem = "the device times no passes";
        }

        _window.Hide();
        _upscaler.Initialize();
        _offscreen.Initialize(PassTarget.Offscreen, BlendMode.None);
        _frame.Initialize(PassTarget.Frame, BlendMode.None);
        _target = _device.MakeFrameTarget(OutputWidth, OutputHeight);

        Paint();
        _device.TimePasses(true);
    }

    public void Frame(double seconds, InputFrame input)
    {
        if (_frames == 0)
        {
            _offscreen.Upload(_picture);
            _frame.Upload(_picture);
        }

        if (_frames < WarmUp + Measured)
        {
            Upscale();
        }
        else
        {
            Bilinear();
        }

        _device.BeginFramePass(Black);
        _device.EndPass();
        _device.Commit();
        Collect();
        _frames++;

        if (_frames >= (2 * (WarmUp + Measured)) || Problem is not null)
        {
            _window.Quit();
        }
    }

    public void DeviceLost()
    {
    }

    public void DeviceRestored()
    {
    }

    public void Cleanup()
    {
        _device.TimePasses(false);
        _device.Destroy(_target);
        _frame.Cleanup();
        _offscreen.Cleanup();
        _upscaler.Cleanup();
    }

    public string Report()
    {
        var fsr = _fsrCount == 0 ? 0 : 1.0 / _fsrCount;
        var bilinear = _bilinearCount == 0 ? 0 : 1.0 / _bilinearCount;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"FSR 1 {PictureWidth}x{PictureHeight} to {OutputWidth}x{OutputHeight} over {_fsrCount} frames: the picture's pass {_fsr[0] * fsr:F3} ms, EASU {_fsr[1] * fsr:F3} ms, RCAS {_fsr[2] * fsr:F3} ms; bilinear as before over {_bilinearCount} frames: {_bilinear[0] * bilinear:F3} ms; managed allocation in the measured frames {Allocated} bytes");
    }

    // The upscaler's three passes as the stream runs them for a new picture, the last into the window-format target.
    private void Upscale()
    {
        var before = GC.GetAllocatedBytesForCurrentThread();

        if (!_upscaler.Plan(PictureWidth, PictureHeight, Whole, OutputWidth, OutputHeight))
        {
            Problem = "the upscaler did not plan a larger picture";

            return;
        }

        _upscaler.BeginPicture();
        _offscreen.Draw(Whole);
        _upscaler.EndPicture();
        _device.BeginTargetPass(_target, Black);
        _upscaler.Draw();
        _device.EndPass();

        if (_frames >= WarmUp)
        {
            Allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }

    private void Bilinear()
    {
        _device.BeginTargetPass(_target, Black);
        _frame.Draw(Whole);
        _device.EndPass();
    }

    // The pass times that came back: an FSR frame's first three, a bilinear frame's first (the window's pass is last).
    private void Collect()
    {
        if (!_device.TryTakePassTimes(out var frame, _times, out var passes))
        {
            return;
        }

        if (passes == 4 && frame >= WarmUp)
        {
            Add(_fsr, 3);
            _fsrCount++;
        }
        else if (passes == 2 && frame >= (2 * WarmUp) + Measured)
        {
            Add(_bilinear, 1);
            _bilinearCount++;
        }
    }

    private void Add(double[] totals, int count)
    {
        for (var index = 0; index < count; index++)
        {
            totals[index] += _times[index];
        }
    }

    // A 1440p picture with detail: luma value noise in 8-pixel cells on a gradient, neutral chroma.
    private void Paint()
    {
        _picture.Prepare(PictureWidth, PictureHeight, PictureWidth / 2, PictureHeight / 2, alpha: false);
        var luma = _picture.Luma;

        for (var y = 0; y < PictureHeight; y++)
        {
            for (var x = 0; x < PictureWidth; x++)
            {
                var noise = (int)((uint)(((x / 8) * 73856093) ^ ((y / 8) * 19349663)) % 96);
                luma[(y * PictureWidth) + x] = (byte)(40 + (x * 80 / PictureWidth) + noise);
            }
        }

        _picture.Cb.Fill(128);
        _picture.Cr.Fill(128);
    }
}
