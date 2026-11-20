// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Engine.Core;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows.Recording;

/// <summary>
/// NV12 pictures on a D3D11 device encoded into an MP4 file on the GPU: Media Foundation's sink writer with hardware
/// transforms allowed and the device's manager (MF_SINK_WRITER_D3D_MANAGER), so on an NVIDIA GPU it runs NVENC through
/// NVIDIA's encoder transform, at a high mean bitrate. A picture is copied on the GPU (<see cref="TryCopy"/>, its top-left
/// <see cref="Width"/> x <see cref="Height"/>) into a texture of the encoder's own small pool, so whatever it was copied
/// from (a shared ring's slot) is not held; the copy is queued, never waited for. Then the writer's thread hands that
/// texture to the encoder with its time and duration (<see cref="TryWrite"/>). A pool texture is free again once the
/// encoder let go of its sample: the sample's reference count is back to the pool's own one (an AddRef and Release
/// read it); none free, and the picture is not taken. One thread copies, one writes; the device must be
/// multithread-protected (the decoder's is). Game-agnostic (an engine candidate).
/// </summary>
public sealed unsafe class Mp4VideoEncoder : IDisposable
{
    /// <summary>The pool's textures: the one held for its duration, those the encoder works on, and room to spare.</summary>
    public const int PoolSize = 8;

    private const int FreeSlot = 0;
    private const int ClaimedSlot = 1;
    private const int SubmittedSlot = 2;
    private const int NoSlot = -1;
    private const int OwnReferenceOnly = 1;

    // NV12's sizes are even, its bytes a picture's luma and half again of chroma.
    private const int ChromaStep = 2;
    private const int Nv12Numerator = 3;
    private const int Nv12Denominator = 2;

    private const uint MaxTransforms = 8;
    private const int NameCharacters = 256;
    private const string UnknownEncoder = "an unnamed encoder";

    private readonly ComWrappers _wrappers = new StrategyBasedComWrappers();
    private readonly nint[] _textures = new nint[PoolSize];
    private readonly nint[] _samples = new nint[PoolSize];
    private readonly int[] _states = new int[PoolSize];
    private readonly ID3D11DeviceContext _contextApi;
    private readonly Box _box;
    private nint _device;
    private nint _context;
    private nint _manager;
    private nint _writer;
    private IMFSinkWriter? _writerApi;
    private uint _stream;
    private int _cursor;
    private bool _started;
    private bool _disposed;

    private Mp4VideoEncoder(nint device, VideoEncoderSettings settings)
    {
        _device = device;
        _ = Marshal.AddRef(device);
        Width = EvenDown(settings.Width);
        Height = EvenDown(settings.Height);
        Codec = settings.Codec;

        var deviceApi = ComObjects.Wrap<ID3D11Device>(_wrappers, device);
        deviceApi.GetImmediateContext(out _context);
        ComObjects.Free(deviceApi);
        _contextApi = ComObjects.Wrap<ID3D11DeviceContext>(_wrappers, _context);
        _box = new Box { Right = (uint)Width, Bottom = (uint)Height, Back = 1 };
    }

    /// <summary>The encoded width: the asked width made even.</summary>
    public int Width { get; }

    /// <summary>The encoded height: the asked height made even.</summary>
    public int Height { get; }

    public RecordedVideoCodec Codec { get; }

    /// <summary>The encoder transform the sink writer chose ("NVIDIA H.264 Encoder MFT").</summary>
    public string EncoderName { get; private set; } = UnknownEncoder;

    /// <summary>Whether that transform is a hardware one.</summary>
    public bool IsHardware { get; private set; }

    /// <summary>
    /// An encoder writing <paramref name="path"/> from textures of <paramref name="device"/> (an ID3D11Device; the encoder
    /// holds its own reference) as <paramref name="settings"/> ask; null and why when Media Foundation refuses.
    /// </summary>
    public static Mp4VideoEncoder? TryCreate(nint device, string path, VideoEncoderSettings settings, out string? problem)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(settings);

        var encoder = new Mp4VideoEncoder(device, settings);
        problem = encoder.Prepare(path, settings);

        if (problem is not null)
        {
            encoder.Dispose();

            return null;
        }

        return encoder;
    }

    /// <summary>
    /// Queues the copy of <paramref name="texture"/>'s subresource <paramref name="subresource"/> (an NV12 texture of the
    /// device, at least <see cref="Width"/> x <see cref="Height"/>) into a free pool texture, <paramref name="slot"/>;
    /// false when none is free. The copying thread's only call.
    /// </summary>
    [HotPath]
    public bool TryCopy(nint texture, uint subresource, out int slot)
    {
        slot = Claim();

        if (slot == NoSlot)
        {
            return false;
        }

        var box = _box;
        _contextApi.CopySubresourceRegion(_textures[slot], 0, 0, 0, 0, texture, subresource, (nint)(&box));
        SharingCalls.Flush(_context);

        return true;
    }

    /// <summary>Encodes pool texture <paramref name="slot"/> at <paramref name="time"/> for <paramref name="duration"/> (100 ns units); false when refused.</summary>
    public bool TryWrite(int slot, long time, long duration)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var api = ComObjects.Wrap<IMFSample>(_wrappers, _samples[slot]);
        var result = RecordingTypes.Next(api.SetSampleTime(time), api.SetSampleDuration(duration));
        ComObjects.Free(api);

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.WriteSample(_stream, _samples[slot]);
        }

        Volatile.Write(ref _states[slot], SubmittedSlot);

        return !ComObjects.Failed(result);
    }

    /// <summary>A copied texture that will not be written after all is free again.</summary>
    public void Abandon(int slot)
    {
        Volatile.Write(ref _states[slot], FreeSlot);
    }

    /// <summary>Drains the encoder and finishes the file; false and why when it could not.</summary>
    public bool TryFinish(out string? problem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        problem = null;
        var result = _writerApi!.FinalizeWriting();

        if (ComObjects.Failed(result))
        {
            problem = $"the encoded MP4 file could not be finished (0x{result:X8})";
        }

        return problem is null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The writer first (it holds the encoder, the manager and the samples), then the pool, then the device.
        ComObjects.Free(_writerApi);
        ComObjects.Release(ref _writer);
        ReleasePool();
        ComObjects.Release(ref _manager);

        ComObjects.Free(_contextApi);
        ComObjects.Release(ref _context);
        ComObjects.Release(ref _device);

        if (_started)
        {
            _ = NativeMethods.MFShutdown();
        }
    }

    private static int EvenDown(int value)
    {
        return value - (value % ChromaStep);
    }

    // A free pool texture, the one after the last first; one the encoder let go of is freed on the way.
    [HotPath]
    private int Claim()
    {
        for (var step = 0; step < PoolSize; step++)
        {
            var candidate = (_cursor + step) % PoolSize;
            Reclaim(candidate);

            if (Interlocked.CompareExchange(ref _states[candidate], ClaimedSlot, FreeSlot) == FreeSlot)
            {
                _cursor = candidate + 1;

                return candidate;
            }
        }

        return NoSlot;
    }

    // A written texture whose sample only the pool holds again is the encoder's no more.
    [HotPath]
    private void Reclaim(int slot)
    {
        if (Volatile.Read(ref _states[slot]) != SubmittedSlot)
        {
            return;
        }

        var sample = _samples[slot];
        _ = Marshal.AddRef(sample);

        if (Marshal.Release(sample) == OwnReferenceOnly)
        {
            _ = Interlocked.CompareExchange(ref _states[slot], FreeSlot, SubmittedSlot);
        }
    }

    // Media Foundation, the device's manager, the pool, then the writer and its stream; why not, or null.
    private string? Prepare(string path, VideoEncoderSettings settings)
    {
        var result = MediaFoundationTransforms.Start();
        _started = !ComObjects.Failed(result);

        if (!_started)
        {
            return $"MFStartup failed (0x{result:X8})";
        }

        if (!D3D11VideoDevice.TryCreateManager(_wrappers, _device, out _manager, out var problem))
        {
            return problem;
        }

        problem = PreparePool();
        problem ??= OpenWriter(path);
        problem ??= AddStream(settings);

        if (problem is null)
        {
            NameEncoder();
        }

        return problem;
    }

    // Each pool texture with its sample: a DXGI surface buffer over it, its length an NV12 picture's.
    private string? PreparePool()
    {
        var bindings = Direct3D11Values.BindShaderResource | Direct3D11Values.BindRenderTarget;

        for (var slot = 0; slot < PoolSize; slot++)
        {
            var result = CreateTexture(bindings, out _textures[slot]);

            if (ComObjects.Failed(result) && slot == 0)
            {
                bindings = Direct3D11Values.BindShaderResource;
                result = CreateTexture(bindings, out _textures[slot]);
            }

            if (!ComObjects.Failed(result))
            {
                result = CreateSample(_textures[slot], out _samples[slot]);
            }

            if (ComObjects.Failed(result))
            {
                return $"the encoder's NV12 texture of {Width}x{Height} could not be made (0x{result:X8})";
            }
        }

        return null;
    }

    private int CreateTexture(uint bindings, out nint texture)
    {
        var description = new Texture2DDescription
        {
            Width = (uint)Width,
            Height = (uint)Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = MediaFoundationValues.Nv12Format,
            SampleCount = 1,
            Usage = Direct3D11Values.DefaultUsage,
            BindFlags = bindings,
        };

        var deviceApi = ComObjects.Wrap<ID3D11Device>(_wrappers, _device);
        var result = deviceApi.CreateTexture2D(in description, 0, out texture);
        ComObjects.Free(deviceApi);

        return result;
    }

    private int CreateSample(nint texture, out nint sample)
    {
        sample = 0;
        var result = NativeMethods.MFCreateDXGISurfaceBuffer(in MediaFoundationIds.Texture2DInterface, texture, 0, 0, out var buffer);

        if (!ComObjects.Failed(result))
        {
            result = NativeMethods.MFCreateSample(out sample);
        }

        if (!ComObjects.Failed(result))
        {
            result = Attach(sample, buffer);
        }

        ComObjects.Release(ref buffer);

        return result;
    }

    // The buffer into the sample (which then holds it), its length set: an encoder reads only what a buffer says it holds.
    private int Attach(nint sample, nint buffer)
    {
        var sampleApi = ComObjects.Wrap<IMFSample>(_wrappers, sample);
        var result = sampleApi.AddBuffer(buffer);
        ComObjects.Free(sampleApi);

        var bufferApi = ComObjects.Wrap<IMFMediaBuffer>(_wrappers, buffer);
        var length = (uint)(Width * Height * Nv12Numerator / Nv12Denominator);
        result = RecordingTypes.Next(result, bufferApi.SetCurrentLength(length));
        ComObjects.Free(bufferApi);

        return result;
    }

    // The sink writer for the file, allowed hardware encoders and given the device's manager.
    private string? OpenWriter(string path)
    {
        var result = RecordingTypes.TryCreateAttributes(_wrappers, out var store, out var attributes);

        if (!ComObjects.Failed(result) && attributes is not null)
        {
            result = RecordingTypes.Next(attributes.SetUInt32(in MediaFoundationIds.EnableHardwareTransforms, 1), attributes.SetUnknown(in MediaFoundationIds.SinkWriterDeviceManager, _manager));
        }

        if (!ComObjects.Failed(result))
        {
            result = NativeMethods.MFCreateSinkWriterFromURL(path, 0, store, out _writer);
        }

        ComObjects.Free(attributes);
        ComObjects.Release(ref store);

        if (ComObjects.Failed(result))
        {
            return $"Media Foundation's sink writer could not open the file (0x{result:X8})";
        }

        _writerApi = ComObjects.Wrap<IMFSinkWriter>(_wrappers, _writer);

        return null;
    }

    // The encoded stream's type, the NV12 input's, the rate control, then writing; why not, or null.
    private string? AddStream(VideoEncoderSettings settings)
    {
        var subtype = MediaFoundationIds.H264Subtype;
        var profile = MediaFoundationValues.H264HighProfile;

        if (Codec == RecordedVideoCodec.Hevc)
        {
            subtype = MediaFoundationIds.HevcSubtype;
            profile = MediaFoundationValues.HevcMainProfile;
        }

        var result = RecordingTypes.TryCreate(_wrappers, in subtype, Width, Height, settings.FramesPerSecond, out var output, out var outputAttributes);

        if (!ComObjects.Failed(result) && outputAttributes is not null)
        {
            result = RecordingTypes.Next(outputAttributes.SetUInt32(in MediaFoundationIds.AverageBitrate, (uint)settings.BitsPerSecond), outputAttributes.SetUInt32(in MediaFoundationIds.Mpeg2Profile, profile));
            result = RecordingTypes.Next(result, RecordingTypes.DescribeBt709(outputAttributes));
        }

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.AddStream(output, out _stream);
        }

        ComObjects.Free(outputAttributes);
        ComObjects.Release(ref output);

        if (ComObjects.Failed(result))
        {
            return $"no {Codec} encoder took {Width}x{Height} (0x{result:X8})";
        }

        return SetInput(settings);
    }

    // NV12 in at the same size and rate, with the mean bitrate as the encoder's rate control where it takes it.
    private string? SetInput(VideoEncoderSettings settings)
    {
        var result = RecordingTypes.TryCreate(_wrappers, in MediaFoundationIds.Nv12Subtype, Width, Height, settings.FramesPerSecond, out var input, out var inputAttributes);

        if (!ComObjects.Failed(result) && inputAttributes is not null)
        {
            result = RecordingTypes.DescribeBt709(inputAttributes);
        }

        if (!ComObjects.Failed(result))
        {
            result = SetInputWithRate(input, settings.BitsPerSecond);
        }

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.BeginWriting();
        }

        ComObjects.Free(inputAttributes);
        ComObjects.Release(ref input);

        if (ComObjects.Failed(result))
        {
            return $"the {Codec} encoder refused NV12 pictures of {Width}x{Height} on the GPU (0x{result:X8})";
        }

        return null;
    }

    // The input type with the encoder's rate control set to the mean bitrate; without it where the encoder refuses that.
    private int SetInputWithRate(nint input, int bitsPerSecond)
    {
        var result = RecordingTypes.TryCreateAttributes(_wrappers, out var store, out var parameters);

        if (!ComObjects.Failed(result) && parameters is not null)
        {
            result = RecordingTypes.Next(parameters.SetUInt32(in MediaFoundationIds.RateControlMode, MediaFoundationValues.UnconstrainedVbr), parameters.SetUInt32(in MediaFoundationIds.MeanBitRate, (uint)bitsPerSecond));
        }

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.SetInputMediaType(_stream, input, store);
        }

        ComObjects.Free(parameters);
        ComObjects.Release(ref store);

        if (ComObjects.Failed(result))
        {
            result = _writerApi!.SetInputMediaType(_stream, input, 0);
        }

        return result;
    }

    // The video encoder among the stream's transforms, for its name and whether it runs on the GPU.
    private void NameEncoder()
    {
        if (_writerApi is not IMFSinkWriterEx extended)
        {
            return;
        }

        for (var index = 0u; index < MaxTransforms; index++)
        {
            if (ComObjects.Failed(extended.GetTransformForStream(_stream, index, out var category, out var transform)))
            {
                return;
            }

            if (category == MediaFoundationIds.VideoEncoderCategory)
            {
                ReadName(transform);
            }

            ComObjects.Release(ref transform);
        }
    }

    private void ReadName(nint transform)
    {
        var api = ComObjects.Wrap<IMFTransform>(_wrappers, transform);

        if (!ComObjects.Failed(api.GetAttributes(out var attributes)))
        {
            var store = ComObjects.Wrap<IMFAttributes>(_wrappers, attributes);
            EncoderName = ReadString(store, in MediaFoundationIds.FriendlyName) ?? UnknownEncoder;
            IsHardware = !ComObjects.Failed(store.GetStringLength(in MediaFoundationIds.HardwareUrl, out _));
            ComObjects.Free(store);
            ComObjects.Release(ref attributes);
        }

        ComObjects.Free(api);
    }

    private static string? ReadString(IMFAttributes store, in Guid key)
    {
        var buffer = stackalloc char[NameCharacters];

        if (ComObjects.Failed(store.GetString(in key, buffer, NameCharacters, out var length)))
        {
            return null;
        }

        return new string(buffer, 0, (int)length);
    }

    private void ReleasePool()
    {
        for (var slot = 0; slot < PoolSize; slot++)
        {
            ComObjects.Release(ref _samples[slot]);
            ComObjects.Release(ref _textures[slot]);
        }
    }
}
