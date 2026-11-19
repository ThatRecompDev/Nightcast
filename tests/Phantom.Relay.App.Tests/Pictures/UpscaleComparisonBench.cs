// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Media.Rendering;
using Phantom.Engine.Media.Video;
using Phantom.Relay.App.Pictures;
using Phantom.Relay.Profiles;
using static Phantom.Relay.Lsfg.Tests.Live.UpscaleTestImages;

namespace Phantom.Relay.App.Tests.Pictures;

/// <summary>
/// A hidden window's frames for <see cref="UpscaleComparisonLiveTests"/>: each test picture (1440p, uploaded as the
/// read-back path uploads a decoded one) drawn into a 2160p target of the window's format bilinearly and with FSR 1 at
/// each of F7's sharpness steps, as <see cref="StreamUpscaler"/> draws it, every result read back (BGRA) by the
/// device's frame capture; the window closes when all are back.
/// </summary>
internal sealed class UpscaleComparisonBench : IApplication
{
    /// <summary>The engine's ways, in order: bilinear, then FSR 1 at RCAS 0.2, 0.1, 0 and 0.5 stops.</summary>
    public static readonly float?[] Sharpnesses = [null, 0.2f, 0.1f, 0f, 0.5f];

    private static readonly Vector4 Whole = new(0f, 0f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private readonly IGraphicsDevice _device;
    private readonly IWindow _window;
    private readonly IReadOnlyList<byte[]> _pictures;
    private readonly StreamUpscaler _upscaler;
    private readonly MovieRenderer _offscreen;
    private readonly MovieRenderer _frame;
    private readonly VideoFrame _picture = new();
    private readonly double[] _times = new double[8];
    private GpuRenderTarget _target;
    private GpuFrameCapture _capture;
    private int _job;
    private int _uploaded = -1;
    private int _frames;

    public UpscaleComparisonBench(IGraphicsDevice device, IWindow window, IReadOnlyList<byte[]> pictures)
    {
        _device = device;
        _window = window;
        _pictures = pictures;

        _upscaler = new StreamUpscaler(device);
        _offscreen = new MovieRenderer(device);
        _frame = new MovieRenderer(device);
        Results = new byte[pictures.Count * Sharpnesses.Length][];
    }

    public string? Problem { get; private set; }

    /// <summary>Each picture's results, picture by picture, in <see cref="Sharpnesses"/>' order: BGRA at 2160p.</summary>
    public byte[]?[] Results { get; }

    /// <summary>FSR 1's passes (picture, EASU, RCAS) summed over the timed frames, and how many.</summary>
    public double FsrMilliseconds { get; private set; }

    public int FsrFrames { get; private set; }

    public void Initialize()
    {
        if (!_device.Capabilities.Has(GpuFeatures.FrameCapture))
        {
            Problem = "the device has no frame capture";
        }

        _window.Hide();

        _upscaler.Initialize();
        _offscreen.Initialize(PassTarget.Offscreen, BlendMode.None);
        _frame.Initialize(PassTarget.Frame, BlendMode.None);
        _target = _device.MakeFrameTarget(TruthWidth, TruthHeight);
        _capture = _device.MakeCapture(TruthWidth, TruthHeight);
        _device.TimePasses(true);
    }

    public void Frame(double seconds, InputFrame input)
    {
        if (_job < Results.Length)
        {
            Draw(_job);
        }

        _device.BeginFramePass(Black);
        _device.EndPass();
        _device.Commit();

        if (_job < Results.Length && _device.RequestCapture(_capture, _target, _job))
        {
            _job++;
        }

        Collect();
        _frames++;

        if (Problem is not null || Results.All(static result => result is not null) || _frames > Results.Length * 20)
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
        _device.Destroy(_capture);
        _device.Destroy(_target);
        _frame.Cleanup();
        _offscreen.Cleanup();
        _upscaler.Cleanup();
    }

    private void Draw(int job)
    {
        var picture = job / Sharpnesses.Length;
        var sharpness = Sharpnesses[job % Sharpnesses.Length];
        Upload(picture);

        if (sharpness is null)
        {
            _device.BeginTargetPass(_target, Black);
            _frame.Draw(Whole);
            _device.EndPass();

            return;
        }

        _upscaler.Use(UpscaleMethod.Fsr);
        _upscaler.UseSharpness(sharpness.Value);
        Assert.True(_upscaler.Plan(PictureWidth, PictureHeight, Whole, TruthWidth, TruthHeight));

        _upscaler.BeginPicture();
        _offscreen.Draw(Whole);
        _upscaler.EndPicture();
        _device.BeginTargetPass(_target, Black);
        _upscaler.Draw();
        _device.EndPass();
    }

    private void Upload(int picture)
    {
        if (picture == _uploaded)
        {
            return;
        }

        var (luma, cb, cr) = ToYuv420(_pictures[picture], PictureWidth, PictureHeight);
        _picture.Prepare(PictureWidth, PictureHeight, PictureWidth / 2, PictureHeight / 2, alpha: false);
        luma.CopyTo(_picture.Luma);
        cb.CopyTo(_picture.Cb);
        cr.CopyTo(_picture.Cr);

        _offscreen.Upload(_picture);
        _frame.Upload(_picture);
        _uploaded = picture;
    }

    private void Collect()
    {
        if (_device.TryTakePassTimes(out _, _times, out var passes) && passes == 4)
        {
            FsrMilliseconds += _times[0] + _times[1] + _times[2];
            FsrFrames++;
        }

        var pixels = new byte[TruthWidth * TruthHeight * 4];

        while (_device.TryTakeCapture(_capture, pixels, out var frame))
        {
            Results[frame] = pixels;
            pixels = new byte[TruthWidth * TruthHeight * 4];
        }
    }
}
