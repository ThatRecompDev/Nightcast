// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// H.264 decoded by Windows' own decoder (Media Foundation's CLSID_CMSH264DecoderMFT) on the GPU through DXVA, behind
/// Phantom.Engine's <see cref="IVideoDecoder"/>, so a head swaps it in for the engine's openh264 <c>H264Decoder</c>.
/// One Annex-B access unit in at a time, with its timestamp; one NV12 picture out, copied into the
/// <see cref="VideoFrame"/>'s Y, Cb and Cr planes. Used from one thread at a time.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> The console streams H.264 Main profile, level 4.2, at 2560 x 1440 and 60 fps, a picture due every
/// 16.7 ms. openh264 on the CPU measured 8.1 ms a picture on average and 41.5 ms at worst (key frames) on the owner's
/// PC, and every millisecond of decoding is a millisecond of input-to-photon latency; the worst case is two and a half
/// frames late. Latency is the owner's first priority.</para>
/// <para><b>Why DXVA.</b> The GPU's fixed-function decoder (NVDEC on the owner's RTX 4090) decodes 1440p H.264 in a
/// small, nearly constant time per picture whatever the frame type, and leaves the CPU free; a key frame costs it little
/// more than any other frame, which takes away the spikes. Media Foundation's own decoder is in every Windows install,
/// so nothing native ships with Relay: the device is a D3D11 one with video support, shared with the decoder through an
/// IMFDXGIDeviceManager.</para>
/// <para><b>Why low-latency mode.</b> By default the decoder fills a reorder queue before it emits anything, as a
/// stream with B-frames needs; the console's stream has none, so that queue would only hold pictures back. With
/// CODECAPI_AVLowLatencyMode (and MF_LOW_LATENCY, the same GUID) set, every picture comes out of the
/// <c>ProcessOutput</c> right after its <c>ProcessInput</c>: this decoder never <see cref="Reorders"/>.</para>
/// <para><b>Expected gain.</b> The decode itself should drop from 8.1 ms average and 41.5 ms worst to about a
/// millisecond with no key-frame spike, the read-back of the picture (<see cref="LastCopyDuration"/>: the GPU copy into
/// a staging texture, its map, and the NV12 split, about 5.5 MB a picture) then being the larger part. Measured by the
/// Live test on the owner's RTX 4090 over 30 synthetic 2560 x 1440 access units: decode 1.2 ms average, 0.7 ms worst
/// after the first (the first, 25 ms, allocates the decoder's surfaces once); read-back 4.2 ms average, 6.7 ms worst.
/// So about 5.5 ms a picture in all against openh264's 8.1, and a worst case near 7 ms against 41.5: no picture is a
/// frame late any more. (The synthetic stream is easier than a game's, so the software path's times there are low.)</para>
/// <para><b>Pictures left on the GPU.</b> <see cref="TryTakeSurface"/> hands the decoded surface on instead of reading
/// it back: a <see cref="SharedSurfaceRing"/> on <see cref="Device"/> copies it into a texture the renderer's device
/// opens, with no CPU copy at all. Opened with the renderer's <see cref="AdapterLuid"/>, the device sits on the
/// renderer's GPU. Measured by the Live test on the owner's RTX 4090: the hand-off (the wait for the renderer, the copy
/// and the fence, queued) costs 0.3 ms of CPU a picture against the read-back's 4.2.</para>
/// <para><b>Fallback.</b> When the DXVA path does not start (no D3D11 video device, a decoder that is not D3D11-aware, a
/// media type the GPU refuses), the same decoder is made again in software mode, and <see cref="FallbackReason"/> says
/// why; <see cref="IsHardwareAccelerated"/> tells the two apart.</para>
/// <para><b>Colour.</b> BT.709 in limited range unless a sequence parameter set the stream sends states otherwise in its
/// VUI (<see cref="H264StreamColour"/>).</para>
/// <para><b>Allocation.</b> The input sample, its buffer and the staging texture are made once and reused. The DXVA path
/// wraps the decoder's output sample and its DXGI buffer for each picture (two small source-generated COM wrappers,
/// released at once), the managed allocation the COM interop leaves: about 2 KB a picture in the Live test, against
/// 0.1 KB on the software path, the test's own bookkeeping included.</para>
/// <para><b>Timing.</b> <see cref="LastDecodeDuration"/> is the decode call (input in, output out), measured on the
/// injected <see cref="TimeProvider"/>. The GPU finishes its decode asynchronously, so the wait for it lands in
/// <see cref="LastCopyDuration"/>, at the map.</para>
/// </remarks>
public sealed unsafe class MediaFoundationH264Decoder : IVideoDecoder
{
    /// <summary>Matroska's codec ID for H.264, as the engine's H264Decoder takes.</summary>
    public const string AvcCodecId = "V_MPEG4/ISO/AVC";

    // A 1440p key frame of the console's is a few hundred kilobytes; the buffer grows for a larger one.
    private const int InitialInputBytes = 2 * 1024 * 1024;
    private const int MaxOutputTypes = 64;
    private const int MaxOutputAttempts = 3;

    private readonly ComWrappers _wrappers;
    private readonly TimeProvider _time;
    private readonly IMFTransform _transformApi;
    private readonly MediaFoundationSampleBuffer _input;
    private readonly D3D11VideoDevice? _device;
    private nint _transform;
    private MediaFoundationSampleBuffer? _output;
    private Nv12Format _format;
    private bool _providesSamples;
    private bool _ready;
    private long _pictureTime;
    private VideoColourSpace? _space;
    private VideoRange _range;
    private nint _surface;
    private uint _surfaceSlice;
    private bool _disposed;

    private MediaFoundationH264Decoder(ComWrappers wrappers, TimeProvider time, nint transform, MediaFoundationSampleBuffer input, D3D11VideoDevice? device, long adapterLuid)
    {
        _wrappers = wrappers;
        _time = time;
        _transform = transform;
        _transformApi = ComObjects.Wrap<IMFTransform>(wrappers, transform);
        _input = input;

        // The GPU's device when DXVA decodes, and the adapter it was asked for.
        _device = device;
        AdapterLuid = adapterLuid;
    }

    public string CodecId => AvcCodecId;

    /// <summary>Never: in low-latency mode each picture comes out of the decode of its own access unit.</summary>
    public bool Reorders => false;

    public string LastError { get; private set; } = string.Empty;

    /// <summary>Whether the GPU decodes (DXVA), not the decoder's software path.</summary>
    public bool IsHardwareAccelerated => _device is not null;

    /// <summary>Why the DXVA path did not start, when hardware was asked for and the decoder fell back to software.</summary>
    public string? FallbackReason { get; private set; }

    /// <summary>How long the last <see cref="Decode(ReadOnlySpan{byte}, long)"/> took.</summary>
    public TimeSpan LastDecodeDuration { get; private set; }

    /// <summary>How long the last <see cref="TakePicture(VideoFrame, out long)"/> took to read the picture back and split it.</summary>
    public TimeSpan LastCopyDuration { get; private set; }

    /// <summary>The LUID of the adapter the GPU decodes on, as it was asked for; 0 for the system's default adapter or software.</summary>
    public long AdapterLuid { get; }

    /// <summary>The D3D11 device DXVA decodes on (an ID3D11Device, borrowed: the decoder's own reference); 0 in software.</summary>
    public nint Device => _device?.Device ?? 0;

    /// <summary>The last picture's visible width.</summary>
    public int PictureWidth => _format.Width;

    /// <summary>The last picture's visible height.</summary>
    public int PictureHeight => _format.Height;

    /// <summary>The colour the last picture is drawn with: its stream's, as its sequence parameter set says, else BT.709 limited.</summary>
    public VideoColour PictureColour => H264StreamColour.Settle(_space, _range);

    /// <summary>
    /// A decoder on the GPU when <paramref name="preferHardware"/> and DXVA starts, else in software (with
    /// <see cref="FallbackReason"/>); null and why when Media Foundation's H.264 decoder cannot be made at all.
    /// </summary>
    public static MediaFoundationH264Decoder? TryOpen(TimeProvider time, bool preferHardware, out string? problem)
    {
        return TryOpen(time, preferHardware, 0, out problem);
    }

    /// <summary>
    /// As <see cref="TryOpen(TimeProvider, bool, out string?)"/>, the GPU's device on the adapter of
    /// <paramref name="adapterLuid"/> (the renderer's, so its pictures can be shared with it; 0 for the default adapter).
    /// </summary>
    public static MediaFoundationH264Decoder? TryOpen(TimeProvider time, bool preferHardware, long adapterLuid, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(time);

        string? fallback = null;

        if (preferHardware)
        {
            var hardware = TryCreate(time, hardware: true, adapterLuid, out fallback);

            if (hardware is not null)
            {
                problem = null;

                return hardware;
            }
        }

        var software = TryCreate(time, hardware: false, 0, out problem);

        if (software is not null)
        {
            software.FallbackReason = fallback;
        }

        return software;
    }

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha)
    {
        return Decode(frame, 0);
    }

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha, long timestampNanoseconds)
    {
        return Decode(frame, timestampNanoseconds);
    }

    /// <summary>Decodes one access unit (Annex-B) shown at <paramref name="timestampNanoseconds"/>; its picture is ready after.</summary>
    [HotPath]
    public VideoDecodeResult Decode(ReadOnlySpan<byte> accessUnit, long timestampNanoseconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var started = _time.GetTimestamp();
        _ready = false;

        if (accessUnit.IsEmpty)
        {
            return VideoDecodeResult.Damaged;
        }

        NoteColour(accessUnit);
        var result = Feed(accessUnit, timestampNanoseconds / MediaFoundationValues.NanosecondsPerUnit);
        LastDecodeDuration = _time.GetElapsedTime(started);

        return result;
    }

    /// <summary>Copies the picture the last decode gave into <paramref name="target"/> with its timestamp; false when none.</summary>
    [HotPath]
    public bool TakePicture(VideoFrame target, out long timestampNanoseconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed, this);

        timestampNanoseconds = 0;

        if (!_ready)
        {
            return false;
        }

        var started = _time.GetTimestamp();
        var copied = CopyPicture(target);
        LastCopyDuration = _time.GetElapsedTime(started);

        if (!copied)
        {
            return Unread();
        }

        timestampNanoseconds = _pictureTime * MediaFoundationValues.NanosecondsPerUnit;
        target.TimestampNanoseconds = timestampNanoseconds;
        _ready = false;

        // The stream's colour as its last sequence parameter set stated it, drawn as BT.709 limited when it said none.
        target.StreamSpace = _space;
        target.StreamRange = _range;
        target.Colour = H264StreamColour.Settle(_space, _range);

        return true;
    }

    public bool TakePicture(VideoFrame target)
    {
        return TakePicture(target, out _);
    }

    /// <summary>
    /// The picture the last decode gave, left on the GPU: slice <paramref name="slice"/> of the decoder's texture array
    /// <paramref name="texture"/> (an ID3D11Texture2D on <see cref="Device"/>, borrowed), valid until the next decode, its
    /// visible size <see cref="PictureWidth"/> x <see cref="PictureHeight"/>. False when there is none, or in software.
    /// Either this or <see cref="TakePicture(VideoFrame, out long)"/> takes a picture, not both.
    /// </summary>
    [HotPath]
    public bool TryTakeSurface(out nint texture, out uint slice)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        texture = _surface;
        slice = _surfaceSlice;

        if (!_ready || !_providesSamples || _surface == 0)
        {
            return false;
        }

        _ready = false;

        return true;
    }

    /// <summary>
    /// The picture the last <see cref="TryTakeSurface"/> handed on, read back into <paramref name="target"/> through the
    /// staging texture (waiting for the GPU), for a screenshot; valid until the next decode. False when there is none.
    /// </summary>
    public bool TryReadSurface(VideoFrame target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_device is null || _surface == 0 || !_device.TryCopy(_surface, _surfaceSlice))
        {
            return false;
        }

        if (!_device.TryRead(_format.Width, _format.Height, target))
        {
            return false;
        }

        target.Colour = PictureColour;

        return true;
    }

    /// <summary>Forgets the stream, for a seek or a lost packet: the next access unit must be a key frame.</summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _ = _transformApi.ProcessMessage(MediaFoundationValues.CommandFlush, 0);
        _ready = false;
        ComObjects.Release(ref _surface);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The decoder first, which holds the device manager, then what it read and wrote, then Media Foundation.
        ComObjects.Release(ref _surface);

        ComObjects.Free(_transformApi);
        ComObjects.Release(ref _transform);
        _input.Dispose();
        _output?.Dispose();
        _device?.Dispose();
        _ = NativeMethods.MFShutdown();
    }

    // One decoder of either kind, set up; null and why when any step fails.
    private static MediaFoundationH264Decoder? TryCreate(TimeProvider time, bool hardware, long adapterLuid, out string? problem)
    {
        var result = MediaFoundationTransforms.Start();

        if (ComObjects.Failed(result))
        {
            problem = $"MFStartup failed (0x{result:X8})";

            return null;
        }

        var wrappers = new StrategyBasedComWrappers();
        var decoder = TryAssemble(wrappers, time, hardware, adapterLuid, out problem);

        if (decoder is null)
        {
            _ = NativeMethods.MFShutdown();

            return null;
        }

        if (!decoder.TryConfigure(out problem))
        {
            decoder.Dispose();

            return null;
        }

        return decoder;
    }

    // The transform, the input sample and (for hardware) the device, before any of them is configured.
    private static MediaFoundationH264Decoder? TryAssemble(ComWrappers wrappers, TimeProvider time, bool hardware, long adapterLuid, out string? problem)
    {
        D3D11VideoDevice? device = null;

        if (hardware)
        {
            device = D3D11VideoDevice.TryCreate(wrappers, adapterLuid, out problem);

            if (device is null)
            {
                return null;
            }
        }

        var input = MediaFoundationSampleBuffer.TryCreate(wrappers, InitialInputBytes, out problem);
        var result = MediaFoundationTransforms.TryCreate(wrappers, in MediaFoundationIds.H264Decoder, out var transform, out var api);
        ComObjects.Free(api);

        if (input is null || ComObjects.Failed(result))
        {
            problem ??= $"Media Foundation's H.264 decoder could not be made (0x{result:X8})";
            input?.Dispose();
            device?.Dispose();
            ComObjects.Release(ref transform);

            return null;
        }

        if (device is null)
        {
            adapterLuid = 0;
        }

        return new MediaFoundationH264Decoder(wrappers, time, transform, input, device, adapterLuid);
    }

    // Low latency, the device, the input and output types, then streaming.
    private bool TryConfigure(out string? problem)
    {
        var result = MediaFoundationTransforms.EnableLowLatency(_wrappers, _transform, _transformApi);

        if (ComObjects.Failed(result))
        {
            problem = $"the decoder refused low-latency mode (0x{result:X8})";

            return false;
        }

        if (!TryAttachDevice(out problem) || !TrySetInputType(out problem) || !TrySelectOutput(out problem))
        {
            return false;
        }

        _ = _transformApi.ProcessMessage(MediaFoundationValues.NotifyBeginStreaming, 0);
        _ = _transformApi.ProcessMessage(MediaFoundationValues.NotifyStartOfStream, 0);

        return true;
    }

    private bool TryAttachDevice(out string? problem)
    {
        problem = null;

        if (_device is null)
        {
            return true;
        }

        if (!MediaFoundationTransforms.TryGetAttribute(_wrappers, _transformApi, in MediaFoundationIds.D3D11Aware, out var aware) || aware == 0)
        {
            problem = "the H.264 decoder is not D3D11-aware";

            return false;
        }

        var result = _transformApi.ProcessMessage(MediaFoundationValues.SetD3DManager, (nuint)_device.Manager);

        if (ComObjects.Failed(result))
        {
            problem = $"the H.264 decoder refused the D3D11 device (0x{result:X8})";

            return false;
        }

        return true;
    }

    private bool TrySetInputType(out string? problem)
    {
        problem = null;
        var result = MediaFoundationTypes.TryCreateVideo(_wrappers, in MediaFoundationIds.H264Subtype, out var type, out var attributes);

        if (!ComObjects.Failed(result) && attributes is not null)
        {
            result = attributes.SetUInt32(in MediaFoundationIds.InterlaceMode, MediaFoundationValues.Progressive);
        }

        if (!ComObjects.Failed(result))
        {
            result = _transformApi.SetInputType(0, type, 0);
        }

        ComObjects.Free(attributes);
        ComObjects.Release(ref type);

        if (ComObjects.Failed(result))
        {
            problem = $"the H.264 decoder refused the H.264 input type (0x{result:X8})";

            return false;
        }

        return true;
    }

    // The first NV12 output type the decoder offers; at the start and again at every stream change.
    private bool TrySelectOutput(out string? problem)
    {
        for (var index = 0u; index < MaxOutputTypes; index++)
        {
            if (ComObjects.Failed(_transformApi.GetOutputAvailableType(0, index, out var type)))
            {
                break;
            }

            var attributes = ComObjects.Wrap<IMFAttributes>(_wrappers, type);
            var chosen = MediaFoundationTypes.IsSubtype(attributes, in MediaFoundationIds.Nv12Subtype);
            var result = 0;
            var read = false;

            if (chosen)
            {
                result = _transformApi.SetOutputType(0, type, 0);
                read = MediaFoundationTypes.TryReadNv12(attributes, out _format);
            }

            ComObjects.Free(attributes);
            ComObjects.Release(ref type);

            if (chosen)
            {
                return TryPrepareOutput(result, read, out problem);
            }
        }

        problem = "the H.264 decoder offers no NV12 output";

        return false;
    }

    // Who provides the output samples (the decoder does on the GPU), and our own buffer when it does not.
    private bool TryPrepareOutput(int setResult, bool read, out string? problem)
    {
        problem = null;
        var result = setResult;

        if (!ComObjects.Failed(result))
        {
            result = _transformApi.GetOutputStreamInfo(0, out var info);
            var provides = (info.Flags & MediaFoundationValues.ProvidesSamples) != 0;
            var canProvide = (info.Flags & MediaFoundationValues.CanProvideSamples) != 0;
            _providesSamples = provides || (_device is not null && canProvide);
            _device?.Forget();
            EnsureOutput((int)info.Size, ref problem);
        }

        if (ComObjects.Failed(result) || !read)
        {
            problem = $"the H.264 decoder's NV12 output could not be set (0x{result:X8})";
        }

        return problem is null;
    }

    private void EnsureOutput(int size, ref string? problem)
    {
        if (_providesSamples || (_output is not null && _output.Capacity >= size))
        {
            return;
        }

        _output?.Dispose();
        _output = MediaFoundationSampleBuffer.TryCreate(_wrappers, size, out problem);
    }

    // The stream's colour, from a sequence parameter set the access unit carries (a key frame's).
    [HotPath]
    private void NoteColour(ReadOnlySpan<byte> accessUnit)
    {
        if (AnnexBUnits.TryFind(accessUnit, AnnexBUnits.SequenceParameterSetType, out var set)
            && H264StreamColour.TryRead(set, out var space, out var range))
        {
            _space = space;
            _range = range;
        }
    }

    [HotPath]
    private VideoDecodeResult Feed(ReadOnlySpan<byte> accessUnit, long time)
    {
        var result = _input.TryWrite(accessUnit, time);

        if (ComObjects.Failed(result))
        {
            return Fail(DecoderStep.Input, result);
        }

        result = _transformApi.ProcessInput(0, _input.Sample, 0);

        if (result == MediaFoundationValues.NotAccepting)
        {
            // A picture was still waiting: take it, then the decoder takes the access unit.
            _ = Pull();
            result = _transformApi.ProcessInput(0, _input.Sample, 0);
        }

        if (ComObjects.Failed(result))
        {
            return Fail(DecoderStep.ProcessInput, result);
        }

        var pulled = Pull();

        if (pulled == VideoDecodeResult.Hidden && _ready)
        {
            return VideoDecodeResult.Picture;
        }

        return pulled;
    }

    // The decoder's output after an input: a picture, nothing yet, or a new format to choose first.
    [HotPath]
    private VideoDecodeResult Pull()
    {
        for (var attempt = 0; attempt < MaxOutputAttempts; attempt++)
        {
            var buffer = default(MftOutputDataBuffer);

            if (!_providesSamples && _output is not null)
            {
                _ = _output.Clear();
                buffer.Sample = _output.Sample;
            }

            var result = _transformApi.ProcessOutput(0, 1, &buffer, out _);
            ComObjects.Release(ref buffer.Events);

            if (result == MediaFoundationValues.StreamChange)
            {
                if (!TrySelectOutput(out _))
                {
                    return Fail(DecoderStep.OutputType, result);
                }

                continue;
            }

            if (result == MediaFoundationValues.NeedMoreInput)
            {
                return VideoDecodeResult.Hidden;
            }

            if (ComObjects.Failed(result))
            {
                return Fail(DecoderStep.ProcessOutput, result);
            }

            return Keep(buffer.Sample);
        }

        return Fail(DecoderStep.OutputType, MediaFoundationValues.StreamChange);
    }

    // A picture came out: in our own buffer, or on the GPU, copied into the staging texture at once.
    [HotPath]
    private VideoDecodeResult Keep(nint sample)
    {
        if (!_providesSamples)
        {
            _pictureTime = _output!.Time();
            _ready = true;

            return VideoDecodeResult.Picture;
        }

        var result = KeepSurface(sample);
        ComObjects.Release(ref sample);

        return result;
    }

    [HotPath]
    private VideoDecodeResult KeepSurface(nint sample)
    {
        var sampleApi = ComObjects.Wrap<IMFSample>(_wrappers, sample);
        _ = sampleApi.GetSampleTime(out _pictureTime);
        var result = sampleApi.GetBufferByIndex(0, out var buffer);
        ComObjects.Free(sampleApi);

        if (ComObjects.Failed(result))
        {
            return Fail(DecoderStep.Surface, result);
        }

        result = Marshal.QueryInterface(buffer, in MediaFoundationIds.DxgiBufferInterface, out var surface);
        ComObjects.Release(ref buffer);

        if (ComObjects.Failed(result))
        {
            return Fail(DecoderStep.Surface, result);
        }

        var surfaceApi = ComObjects.Wrap<IMFDXGIBuffer>(_wrappers, surface);
        result = surfaceApi.GetResource(in MediaFoundationIds.Texture2DInterface, out var texture);
        _ = surfaceApi.GetSubresourceIndex(out var slice);
        ComObjects.Free(surfaceApi);
        ComObjects.Release(ref surface);

        if (ComObjects.Failed(result))
        {
            return Fail(DecoderStep.Surface, result);
        }

        // The surface stays the picture's until the next one: read back (TakePicture) or handed on (TryTakeSurface).
        ComObjects.Release(ref _surface);
        _surface = texture;
        _surfaceSlice = slice;
        _ready = true;

        return VideoDecodeResult.Picture;
    }

    [HotPath]
    private bool CopyPicture(VideoFrame target)
    {
        if (_providesSamples)
        {
            return _device!.TryCopy(_surface, _surfaceSlice) && _device.TryRead(_format.Width, _format.Height, target);
        }

        return _output!.TryCopyNv12(_format, target);
    }

    private bool Unread()
    {
        LastError = "the decoded picture could not be read back";
        _ready = false;

        return false;
    }

    private VideoDecodeResult Fail(DecoderStep step, int result)
    {
        LastError = $"Media Foundation's H.264 decoder failed at {step} (0x{result:X8})";

        return VideoDecodeResult.Damaged;
    }
}
