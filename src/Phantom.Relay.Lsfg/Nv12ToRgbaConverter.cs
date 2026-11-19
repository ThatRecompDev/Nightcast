// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Engine.Core;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg;

/// <summary>
/// Turns the hardware decoder's NV12 surfaces into an R8G8B8A8 texture the frame generation reads, with D3D11's
/// video processor (ID3D11VideoContext::VideoProcessorBlt): the GPU's fixed-function colour conversion, BT.709
/// limited range in, full-range RGB out. No shader is written or compiled for it. The input views of the decoder's
/// surfaces are made once per surface and kept.
/// </summary>
public sealed unsafe class Nv12ToRgbaConverter : IDisposable
{
    // The decoder's pool of surfaces is far smaller; views beyond this are made and dropped per call.
    private const int MaxCachedViews = 32;

    // The frame rate the content description states; the processor converts one frame per call either way.
    private const uint NominalRate = 60;

    private readonly ComWrappers _wrappers;
    private readonly ID3D11VideoDevice _videoApi;
    private readonly ID3D11VideoContext _contextApi;
    private readonly (nint Texture, uint Slice, nint View)[] _views = new (nint, uint, nint)[MaxCachedViews];
    private nint _video;
    private nint _context;
    private nint _enumerator;
    private nint _processor;
    private nint _output;
    private nint _outputView;
    private int _cached;

    private Nv12ToRgbaConverter(ComWrappers wrappers, nint video, nint context, nint enumerator, nint processor, int width, int height)
    {
        _wrappers = wrappers;
        _video = video;
        _context = context;
        _enumerator = enumerator;
        _processor = processor;

        Width = width;
        Height = height;
        _videoApi = ComObjects.Wrap<ID3D11VideoDevice>(wrappers, video);
        _contextApi = ComObjects.Wrap<ID3D11VideoContext>(wrappers, context);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The converted picture: an R8G8B8A8_UNORM texture (shader resource and render target) the converter owns.</summary>
    public nint Output => _output;

    /// <summary>A converter for pictures of the given size on <paramref name="device"/>; null and why when D3D11 refuses.</summary>
    public static Nv12ToRgbaConverter? TryCreate(nint device, int width, int height, out string? problem)
    {
        var wrappers = new StrategyBasedComWrappers();

        if (!TryOpenVideo(wrappers, device, out var video, out var context, out problem))
        {
            return null;
        }

        var converter = new Nv12ToRgbaConverter(wrappers, video, context, 0, 0, width, height);
        problem = converter.Prepare(device);

        if (problem is not null)
        {
            converter.Dispose();

            return null;
        }

        return converter;
    }

    /// <summary>Converts one decoded surface (a slice of the decoder's texture array) into <see cref="Output"/>.</summary>
    [HotPath]
    public bool Convert(nint nv12Texture, uint arraySlice)
    {
        var view = ViewOf(nv12Texture, arraySlice, out var owned);

        if (view == 0)
        {
            return false;
        }

        VideoProcessorStream stream = default;
        stream.Enable = 1;
        stream.InputSurface = view;
        var result = _contextApi.VideoProcessorBlt(_processor, _outputView, 0, 1, &stream);

        if (owned)
        {
            ComObjects.Release(ref view);
        }

        return !ComObjects.Failed(result);
    }

    public void Dispose()
    {
        for (var index = 0; index < _cached; index++)
        {
            var view = _views[index].View;
            ComObjects.Release(ref view);
        }

        _cached = 0;
        ComObjects.Free(_videoApi);
        ComObjects.Free(_contextApi);
        ComObjects.Release(ref _outputView);
        ComObjects.Release(ref _output);

        ComObjects.Release(ref _processor);
        ComObjects.Release(ref _enumerator);
        ComObjects.Release(ref _context);
        ComObjects.Release(ref _video);
    }

    private static bool TryOpenVideo(ComWrappers wrappers, nint device, out nint video, out nint context, out string? problem)
    {
        context = 0;
        var result = Marshal.QueryInterface(device, in Direct3D11Values.VideoDeviceInterface, out video);

        if (ComObjects.Failed(result))
        {
            problem = $"the D3D11 device has no video side (0x{result:X8}); create it with D3D11_CREATE_DEVICE_VIDEO_SUPPORT";

            return false;
        }

        var deviceApi = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        deviceApi.GetImmediateContext(out var immediate);
        ComObjects.Free(deviceApi);
        result = Marshal.QueryInterface(immediate, in Direct3D11Values.VideoContextInterface, out context);
        ComObjects.Release(ref immediate);

        if (ComObjects.Failed(result))
        {
            ComObjects.Release(ref video);
            problem = $"the D3D11 context has no video side (0x{result:X8})";

            return false;
        }

        problem = null;

        return true;
    }

    // The processor for this size, its RGBA output texture and view, and the colour spaces of both sides.
    private string? Prepare(nint device)
    {
        var content = new VideoProcessorContentDescription
        {
            InputFrameFormat = Direct3D11Values.ProgressiveFrames,
            InputRateNumerator = NominalRate,
            InputRateDenominator = 1,
            InputWidth = (uint)Width,
            InputHeight = (uint)Height,
            OutputRateNumerator = NominalRate,
            OutputRateDenominator = 1,
            OutputWidth = (uint)Width,
            OutputHeight = (uint)Height,
            Usage = Direct3D11Values.OptimalSpeedUsage,
        };

        if (ComObjects.Failed(_videoApi.CreateVideoProcessorEnumerator(in content, out _enumerator)) || ComObjects.Failed(_videoApi.CreateVideoProcessor(_enumerator, 0, out _processor)))
        {
            return "D3D11 has no video processor for NV12 at this size";
        }

        var problem = CreateOutput(device);

        if (problem is not null)
        {
            return problem;
        }

        _contextApi.VideoProcessorSetStreamFrameFormat(_processor, 0, Direct3D11Values.ProgressiveFrames);
        _contextApi.VideoProcessorSetStreamColorSpace(_processor, 0, Direct3D11Values.Bt709LimitedColourSpace);
        _contextApi.VideoProcessorSetOutputColorSpace(_processor, Direct3D11Values.FullRangeRgbColourSpace);
        _contextApi.VideoProcessorSetStreamAutoProcessingMode(_processor, 0, 0);

        return null;
    }

    private string? CreateOutput(nint device)
    {
        var description = new Texture2DDescription
        {
            Width = (uint)Width,
            Height = (uint)Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Direct3D11Values.Rgba8Format,
            SampleCount = 1,
            BindFlags = Direct3D11Values.BindRenderTarget | Direct3D11Values.BindShaderResource,
        };

        var deviceApi = ComObjects.Wrap<ID3D11Device>(_wrappers, device);
        var result = deviceApi.CreateTexture2D(in description, 0, out _output);
        ComObjects.Free(deviceApi);

        if (ComObjects.Failed(result))
        {
            return $"D3D11 refused the RGBA output texture (0x{result:X8})";
        }

        var view = new VideoProcessorOutputViewDescription { ViewDimension = Direct3D11Values.VideoOutputTexture2D };
        result = _videoApi.CreateVideoProcessorOutputView(_output, _enumerator, in view, out _outputView);

        if (ComObjects.Failed(result))
        {
            return $"D3D11 refused the video processor's output view (0x{result:X8})";
        }

        return null;
    }

    // A surface's input view: from the cache, else made (and cached while there is room; otherwise owned by the caller).
    [HotPath]
    private nint ViewOf(nint texture, uint slice, out bool owned)
    {
        owned = false;

        for (var index = 0; index < _cached; index++)
        {
            if (_views[index].Texture == texture && _views[index].Slice == slice)
            {
                return _views[index].View;
            }
        }

        VideoProcessorInputViewDescription description = default;
        description.ViewDimension = Direct3D11Values.VideoInputTexture2D;
        description.ArraySlice = slice;

        if (ComObjects.Failed(_videoApi.CreateVideoProcessorInputView(texture, _enumerator, in description, out var view)))
        {
            return 0;
        }

        if (_cached < MaxCachedViews)
        {
            _views[_cached] = (texture, slice, view);
            _cached++;
        }
        else
        {
            owned = true;
        }

        return view;
    }
}
