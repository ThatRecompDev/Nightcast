// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Core;
using Phantom.Engine.Graphics.Canvas;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Graphics.Shaders;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.App.Pictures;

/// <summary>
/// The stream's picture shown larger than it came (the console sends 1440p at most; a 4K window shows it at 1.5x) with
/// the engine's upscale shader (<see cref="BuiltInShader.Upscale"/>, as the engine's <see cref="PictureUpscaler"/> uses
/// it) instead of bilinear sampling: the picture (decoded or generated, still NV12) is drawn at its own size into a
/// target (<see cref="BeginPicture"/>), FSR 1's EASU scales it to the letterboxed rectangle's size in a second target
/// (<see cref="EndPicture"/>), and RCAS sharpens it straight into that rectangle of the window's pass
/// (<see cref="Draw"/>); xBR is one pass from the picture into the window. The targets are kept and made again only when
/// the picture's or the rectangle's size changes, and the two first passes run only for a new picture, so a refresh
/// that shows the same picture costs the last pass alone. Unlike <see cref="PictureUpscaler"/>, which makes its targets
/// per call for a picture upscaled once, nothing is made per frame (an engine candidate: a per-frame mode of it).
/// Lossless Scaling's upscalers run where the pictures are decoded and hand them over at the rectangle's size, which then
/// needs nothing here; until they do, or where they cannot run, such a picture is upscaled with FSR 1. RCAS's sharpness
/// is <see cref="Sharpness"/> (F7 steps it, <see cref="NextSharpness"/>).
/// </summary>
public sealed class StreamUpscaler
{
    private const float EasuMode = 0f;
    private const float RcasMode = 1f;
    private const float XbrMode = 2f;
    private const int Corners = 6;
    private const float Half = 0.5f;
    private const int UniformSlot = 1;

    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private readonly IGraphicsDevice _device;
    private GpuShader _shader;
    private GpuPipeline _offscreen;
    private GpuPipeline _frame;
    private GpuSampler _nearest;
    private GpuRenderTarget _picture;
    private GpuRenderTarget _scaled;
    private GpuBindings _pictureBindings;
    private GpuBindings _scaledBindings;
    private UpscaleUniforms _easu;
    private UpscaleUniforms _rcas;
    private UpscaleUniforms _xbr;
    private bool _hasPicture;
    private bool _hasScaled;
    private bool _made;
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private int _windowWidth;
    private int _windowHeight;

    public StreamUpscaler(IGraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        _device = device;
    }

    /// <summary>F7's RCAS sharpness steps, in stops (0 sharpest): the engine's default, then sharper, then softer.</summary>
    private static readonly float[] SharpnessSteps = [PictureUpscaler.FsrSharpness, 0.1f, 0f, 0.5f];

    /// <summary>F6's order: FSR 1, Lossless Scaling's LS1, its performance setting and SGSR, xBR, then off.</summary>
    private static readonly UpscaleMethod[] MethodSteps = [UpscaleMethod.Fsr, UpscaleMethod.Ls1, UpscaleMethod.Ls1Performance, UpscaleMethod.Sgsr, UpscaleMethod.Xbr, UpscaleMethod.Bilinear];

    /// <summary>The filter now: FSR 1 by default; <see cref="UpscaleMethod.Bilinear"/> leaves the picture as it was drawn.</summary>
    public UpscaleMethod Method { get; private set; } = UpscaleMethod.Fsr;

    /// <summary>RCAS's sharpness in stops (0 sharpest), <see cref="PictureUpscaler.FsrSharpness"/> unless F7 changed it.</summary>
    public float Sharpness { get; private set; } = PictureUpscaler.FsrSharpness;

    /// <summary>The filter the engine runs: the method's own, or FSR 1 for a picture a decoder-side method has not upscaled.</summary>
    public UpscaleMethod EngineMethod { get; private set; } = UpscaleMethod.Fsr;

    /// <summary>True when the last <see cref="Plan"/> made the targets anew, so the picture must be drawn into them again.</summary>
    public bool IsStale { get; private set; }

    /// <summary>The rectangle's size the picture is scaled to, as last planned.</summary>
    public int OutputWidth => _width;

    public int OutputHeight => _height;

    /// <summary>F6's order: FSR 1, LS1, LS1 performance, SGSR, xBR, off, then FSR 1 again.</summary>
    public static UpscaleMethod Next(UpscaleMethod method)
    {
        return MethodSteps[(Array.IndexOf(MethodSteps, method) + 1) % MethodSteps.Length];
    }

    /// <summary>The size in pixels of <paramref name="destination"/> (left, top, right, bottom in 0..1) of the window, rounded as <see cref="Plan"/> rounds it.</summary>
    public static (int Width, int Height) RectangleSize(Vector4 destination, int windowWidth, int windowHeight)
    {
        var x = (int)((destination.X * windowWidth) + Half);
        var y = (int)((destination.Y * windowHeight) + Half);
        var width = (int)((destination.Z * windowWidth) + Half) - x;
        var height = (int)((destination.W * windowHeight) + Half) - y;

        return (width, height);
    }

    /// <summary>F7's order: 0.2 (the engine's default), 0.1, 0 (sharpest), 0.5 (softer), then 0.2 again.</summary>
    public static float NextSharpness(float sharpness)
    {
        return SharpnessSteps[(Array.IndexOf(SharpnessSteps, sharpness) + 1) % SharpnessSteps.Length];
    }

    public void Initialize()
    {
        _shader = _device.MakeShader(BuiltInShader.Upscale);
        var settings = new PipelineSettings(0, [], IndexFormat.None, false, false);
        _offscreen = _device.MakePipeline(settings with { Target = PassTarget.Offscreen }, _shader);
        _frame = _device.MakePipeline(settings, _shader);
        _nearest = _device.MakeSampler(new SamplerSettings(false, false, false, false, 1));
        _made = true;
    }

    /// <summary>Uses <paramref name="method"/> from the next frame on (the old name of FSR 1, <see cref="UpscaleMethod.Sharp"/>, is FSR 1).</summary>
    public void Use(UpscaleMethod method)
    {
        Method = method;

        if (method == UpscaleMethod.Sharp)
        {
            Method = UpscaleMethod.Fsr;
        }

        EngineMethod = Method;

        if (Method.RunsOnDecoder())
        {
            EngineMethod = UpscaleMethod.Fsr;
        }

        IsStale = true;
    }

    /// <summary>RCAS sharpens by <paramref name="sharpness"/> stops (0 sharpest) from the next frame on.</summary>
    public void UseSharpness(float sharpness)
    {
        Sharpness = sharpness;

        if (_hasScaled)
        {
            _rcas = Uniforms(_width, _height, RcasMode, 1f, sharpness);
        }
    }

    /// <summary>
    /// Whether this frame upscales: a filter is chosen and the picture (<paramref name="pictureWidth"/> x
    /// <paramref name="pictureHeight"/>) shows larger than it is in <paramref name="destination"/> (left, top, right,
    /// bottom in 0..1) of the window; the targets are made when a size changed (<see cref="IsStale"/>).
    /// </summary>
    [HotPath]
    public bool Plan(int pictureWidth, int pictureHeight, Vector4 destination, int windowWidth, int windowHeight)
    {
        var x = (int)((destination.X * windowWidth) + Half);
        var y = (int)((destination.Y * windowHeight) + Half);
        var width = (int)((destination.Z * windowWidth) + Half) - x;
        var height = (int)((destination.W * windowHeight) + Half) - y;
        (_x, _y, _windowWidth, _windowHeight) = (x, y, windowWidth, windowHeight);

        if (!_made || Method == UpscaleMethod.Bilinear || pictureWidth <= 0 || width <= pictureWidth || height <= pictureHeight)
        {
            return false;
        }

        if (!_hasPicture || _picture.Width != pictureWidth || _picture.Height != pictureHeight || width != _width || height != _height)
        {
            Remake(pictureWidth, pictureHeight, width, height);
        }

        return true;
    }

    /// <summary>Starts the pass that draws the picture at its own size (<see cref="PassTarget.Offscreen"/> renderers, the whole target).</summary>
    [HotPath]
    public void BeginPicture()
    {
        _device.BeginTargetPass(_picture, Black);
    }

    /// <summary>Ends the picture's pass and, for FSR 1, scales it into the rectangle's size (EASU).</summary>
    [HotPath]
    public void EndPicture()
    {
        _device.EndPass();
        IsStale = false;

        if (EngineMethod != UpscaleMethod.Fsr)
        {
            return;
        }

        _device.BeginTargetPass(_scaled, Black);
        _device.ApplyPipeline(_offscreen);
        Apply(_pictureBindings, _easu);
        _device.Draw(0, Corners, 1);
        _device.EndPass();
    }

    /// <summary>In the window's pass: the last step into the rectangle (RCAS, or xBR from the picture), then the whole window again.</summary>
    [HotPath]
    public void Draw()
    {
        _device.ApplyViewport(_x, _y, _width, _height);
        _device.ApplyPipeline(_frame);

        if (EngineMethod == UpscaleMethod.Fsr)
        {
            Apply(_scaledBindings, _rcas);
        }
        else
        {
            Apply(_pictureBindings, _xbr);
        }

        _device.Draw(0, Corners, 1);
        _device.ApplyViewport(0, 0, _windowWidth, _windowHeight);
    }

    public void Cleanup()
    {
        if (!_made)
        {
            return;
        }

        Destroy();
        _device.Destroy(_nearest);
        _device.Destroy(_frame);
        _device.Destroy(_offscreen);
        _device.Destroy(_shader);
        _made = false;
    }

    [HotPath]
    private void Apply(in GpuBindings bindings, in UpscaleUniforms uniforms)
    {
        _device.ApplyBindings(bindings);
        _device.ApplyUniforms(UniformSlot, uniforms);
    }

    // A pass's uniforms: the source's size and inverse, the filter, RCAS's sharpness and the scale.
    private static UpscaleUniforms Uniforms(int width, int height, float mode, float scale, float sharpness)
    {
        return new UpscaleUniforms
        {
            Size = new Vector4(width, height, 1f / width, 1f / height),
            Mode = new Vector4(mode, sharpness, scale, 0f),
        };
    }

    // The picture's target at its size and the scaled one at the rectangle's; the old ones go first.
    private void Remake(int pictureWidth, int pictureHeight, int width, int height)
    {
        Destroy();
        _picture = _device.MakeRenderTarget(pictureWidth, pictureHeight);
        _scaled = _device.MakeRenderTarget(width, height);
        (_hasPicture, _hasScaled) = (true, true);
        (_width, _height) = (width, height);
        IsStale = true;

        // Everything the passes bind, made once for these sizes.
        _pictureBindings = new GpuBindings(default, default, _picture.Texture, _nearest);
        _scaledBindings = new GpuBindings(default, default, _scaled.Texture, _nearest);
        var scale = width / (float)pictureWidth;
        _easu = Uniforms(pictureWidth, pictureHeight, EasuMode, scale, Sharpness);
        _rcas = Uniforms(width, height, RcasMode, 1f, Sharpness);
        _xbr = Uniforms(pictureWidth, pictureHeight, XbrMode, scale, Sharpness);
    }

    private void Destroy()
    {
        if (_hasPicture)
        {
            _device.Destroy(_picture);
        }

        if (_hasScaled)
        {
            _device.Destroy(_scaled);
        }

        (_hasPicture, _hasScaled) = (false, false);
    }
}
