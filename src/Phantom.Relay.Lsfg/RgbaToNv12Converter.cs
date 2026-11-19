// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices.Marshalling;
using Phantom.Engine.Core;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg;

/// <summary>
/// Turns a generated R8G8B8A8 frame back into NV12, into a texture the renderer draws as it draws a decoded picture (a
/// slot of the decoder's shared ring), with D3D11's video processor: full-range RGB in, BT.709 limited range out, the
/// inverse of <see cref="Nv12ToRgbaConverter"/>, fixed-function, no shader. So generated and decoded pictures take one
/// path to the screen (the engine's NV12 picture renderer, no alpha to mind). The views of each input and each target
/// are made once and kept; the target must be bound as a render target.
/// </summary>
public sealed unsafe class RgbaToNv12Converter : IDisposable
{
    // The generator's outputs and the ring's slots are a handful; views beyond this are made and dropped per call.
    private const int MaxCachedViews = 16;

    // E_FAIL: a view D3D11 would not make, so nothing was converted.
    private const int NotConverted = unchecked((int)0x80004005);

    private readonly ID3D11VideoDevice _videoApi;
    private readonly ID3D11VideoContext _contextApi;
    private readonly (nint Texture, nint View)[] _inputs = new (nint, nint)[MaxCachedViews];
    private readonly (nint Texture, nint View)[] _outputs = new (nint, nint)[MaxCachedViews];
    private nint _video;
    private nint _context;
    private nint _enumerator;
    private nint _processor;
    private int _inputCount;
    private int _outputCount;

    private RgbaToNv12Converter(StrategyBasedComWrappers wrappers, nint video, nint context, int width, int height)
    {
        _video = video;
        _context = context;
        _videoApi = ComObjects.Wrap<ID3D11VideoDevice>(wrappers, video);
        _contextApi = ComObjects.Wrap<ID3D11VideoContext>(wrappers, context);
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>A converter for frames of the given size on <paramref name="device"/>; null and why when D3D11 refuses.</summary>
    public static RgbaToNv12Converter? TryCreate(nint device, int width, int height, out string? problem)
    {
        var wrappers = new StrategyBasedComWrappers();

        if (!VideoProcessorSides.TryOpen(wrappers, device, out var video, out var context, out problem))
        {
            return null;
        }

        var converter = new RgbaToNv12Converter(wrappers, video, context, width, height);

        if (!VideoProcessorSides.TryCreateProcessor(converter._videoApi, width, height, out converter._enumerator, out converter._processor))
        {
            problem = "D3D11 has no video processor from RGBA to NV12 at this size";
            converter.Dispose();

            return null;
        }

        converter.SetColourSpaces();

        return converter;
    }

    /// <summary>Converts <paramref name="rgba"/> (an R8G8B8A8 texture) into <paramref name="nv12"/> (an NV12 render target); false when D3D11 refuses either.</summary>
    [HotPath]
    public bool Convert(nint rgba, nint nv12)
    {
        var input = InputView(rgba, out var ownedInput);
        var output = OutputView(nv12, out var ownedOutput);
        var result = NotConverted;

        if (input != 0 && output != 0)
        {
            VideoProcessorStream stream = default;
            stream.Enable = 1;
            stream.InputSurface = input;
            result = _contextApi.VideoProcessorBlt(_processor, output, 0, 1, &stream);
        }

        if (ownedInput)
        {
            ComObjects.Release(ref input);
        }

        if (ownedOutput)
        {
            ComObjects.Release(ref output);
        }

        return !ComObjects.Failed(result);
    }

    public void Dispose()
    {
        Release(_inputs, ref _inputCount);
        Release(_outputs, ref _outputCount);
        ComObjects.Free(_videoApi);
        ComObjects.Free(_contextApi);
        ComObjects.Release(ref _processor);
        ComObjects.Release(ref _enumerator);

        ComObjects.Release(ref _context);
        ComObjects.Release(ref _video);
    }

    private static void Release((nint Texture, nint View)[] views, ref int count)
    {
        for (var index = 0; index < count; index++)
        {
            ComObjects.Release(ref views[index].View);
        }

        count = 0;
    }

    private void SetColourSpaces()
    {
        _contextApi.VideoProcessorSetStreamFrameFormat(_processor, 0, Direct3D11Values.ProgressiveFrames);
        _contextApi.VideoProcessorSetStreamColorSpace(_processor, 0, Direct3D11Values.FullRangeRgbColourSpace);
        _contextApi.VideoProcessorSetOutputColorSpace(_processor, Direct3D11Values.Bt709LimitedColourSpace);
        _contextApi.VideoProcessorSetStreamAutoProcessingMode(_processor, 0, 0);
    }

    [HotPath]
    private nint InputView(nint texture, out bool owned)
    {
        owned = false;

        for (var index = 0; index < _inputCount; index++)
        {
            if (_inputs[index].Texture == texture)
            {
                return _inputs[index].View;
            }
        }

        VideoProcessorInputViewDescription description = default;
        description.ViewDimension = Direct3D11Values.VideoInputTexture2D;

        if (ComObjects.Failed(_videoApi.CreateVideoProcessorInputView(texture, _enumerator, in description, out var view)))
        {
            return 0;
        }

        owned = !Keep(_inputs, ref _inputCount, texture, view);

        return view;
    }

    [HotPath]
    private nint OutputView(nint texture, out bool owned)
    {
        owned = false;

        for (var index = 0; index < _outputCount; index++)
        {
            if (_outputs[index].Texture == texture)
            {
                return _outputs[index].View;
            }
        }

        VideoProcessorOutputViewDescription description = default;
        description.ViewDimension = Direct3D11Values.VideoOutputTexture2D;

        if (ComObjects.Failed(_videoApi.CreateVideoProcessorOutputView(texture, _enumerator, in description, out var view)))
        {
            return 0;
        }

        owned = !Keep(_outputs, ref _outputCount, texture, view);

        return view;
    }

    private static bool Keep((nint Texture, nint View)[] views, ref int count, nint texture, nint view)
    {
        if (count == views.Length)
        {
            return false;
        }

        views[count] = (texture, view);
        count++;

        return true;
    }
}
