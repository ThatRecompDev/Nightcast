// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// One Media Foundation sample over one system-memory buffer, made once and reused for every frame: the decoder's input
/// (an access unit written in, <see cref="TryWrite"/>) or, when the decoder does not provide its own, its NV12 output
/// (<see cref="TryCopyNv12"/>). A larger access unit than it holds makes both again, twice the size.
/// </summary>
internal sealed unsafe class MediaFoundationSampleBuffer : IDisposable
{
    private const int GrowthFactor = 2;

    private readonly ComWrappers _wrappers;
    private nint _sample;
    private nint _buffer;
    private IMFSample _sampleApi;
    private IMFMediaBuffer _bufferApi;

    private MediaFoundationSampleBuffer(ComWrappers wrappers, nint sample, nint buffer, int capacity)
    {
        _wrappers = wrappers;
        _sample = sample;
        _buffer = buffer;
        _sampleApi = ComObjects.Wrap<IMFSample>(wrappers, sample);
        _bufferApi = ComObjects.Wrap<IMFMediaBuffer>(wrappers, buffer);
        Capacity = capacity;
    }

    /// <summary>The sample to hand the transform.</summary>
    public nint Sample => _sample;

    /// <summary>The bytes the buffer holds.</summary>
    public int Capacity { get; private set; }

    /// <summary>A sample over a buffer of <paramref name="capacity"/> bytes; null and why when Media Foundation refuses.</summary>
    public static MediaFoundationSampleBuffer? TryCreate(ComWrappers wrappers, int capacity, out string? problem)
    {
        var result = TryMake(wrappers, capacity, out var sample, out var buffer);

        if (ComObjects.Failed(result))
        {
            problem = $"a Media Foundation sample of {capacity} bytes could not be made (0x{result:X8})";

            return null;
        }

        problem = null;

        return new MediaFoundationSampleBuffer(wrappers, sample, buffer, capacity);
    }

    /// <summary>The sample's time, in Media Foundation's 100 ns units.</summary>
    [HotPath]
    public long Time()
    {
        if (ComObjects.Failed(_sampleApi.GetSampleTime(out var time)))
        {
            return 0;
        }

        return time;
    }

    /// <summary>Empties the buffer before the transform writes into it again (it refuses a buffer still holding a picture).</summary>
    [HotPath]
    public int Clear()
    {
        return _bufferApi.SetCurrentLength(0);
    }

    /// <summary>Writes an access unit and its time (100 ns units) into the sample; the HRESULT of the first step that failed.</summary>
    [HotPath]
    public int TryWrite(ReadOnlySpan<byte> accessUnit, long time)
    {
        if (accessUnit.Length > Capacity)
        {
            var grown = Grow(accessUnit.Length);

            if (ComObjects.Failed(grown))
            {
                return grown;
            }
        }

        var result = _bufferApi.Lock(out var data, out _, out _);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        accessUnit.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef<byte>((void*)data), Capacity));
        _ = _bufferApi.Unlock();
        result = _bufferApi.SetCurrentLength((uint)accessUnit.Length);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        return _sampleApi.SetSampleTime(time);
    }

    /// <summary>Splits the NV12 picture the decoder wrote into the buffer into the frame's planes; false when it does not fit.</summary>
    [HotPath]
    public bool TryCopyNv12(Nv12Format format, VideoFrame target)
    {
        var lumaBytes = (long)format.Stride * format.CodedHeight;
        var chromaBytes = (long)format.Stride * VideoFrame.ChromaSide(format.CodedHeight, 1);

        if (lumaBytes + chromaBytes > Capacity || ComObjects.Failed(_bufferApi.Lock(out var data, out _, out _)))
        {
            return false;
        }

        try
        {
            var luma = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>((void*)data), (int)lumaBytes);
            var chroma = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>((byte*)data + lumaBytes), (int)chromaBytes);
            Nv12Planes.CopyToFrame(luma, chroma, format.Stride, format.Width, format.Height, target);
        }
        finally
        {
            _ = _bufferApi.Unlock();
        }

        return true;
    }

    public void Dispose()
    {
        ComObjects.Free(_sampleApi);
        ComObjects.Free(_bufferApi);
        ComObjects.Release(ref _buffer);
        ComObjects.Release(ref _sample);
    }

    private static int TryMake(ComWrappers wrappers, int capacity, out nint sample, out nint buffer)
    {
        buffer = 0;
        var result = NativeMethods.MFCreateSample(out sample);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        result = NativeMethods.MFCreateMemoryBuffer((uint)capacity, out buffer);

        if (ComObjects.Failed(result))
        {
            ComObjects.Release(ref sample);

            return result;
        }

        var api = ComObjects.Wrap<IMFSample>(wrappers, sample);
        result = api.AddBuffer(buffer);
        ComObjects.Free(api);

        if (ComObjects.Failed(result))
        {
            ComObjects.Release(ref buffer);
            ComObjects.Release(ref sample);
        }

        return result;
    }

    // A larger buffer (an access unit beyond any before): a new sample and buffer, the old ones released.
    private int Grow(int length)
    {
        var capacity = Math.Max(length, Capacity * GrowthFactor);
        var result = TryMake(_wrappers, capacity, out var sample, out var buffer);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        Dispose();
        _sample = sample;
        _buffer = buffer;
        _sampleApi = ComObjects.Wrap<IMFSample>(_wrappers, sample);
        _bufferApi = ComObjects.Wrap<IMFMediaBuffer>(_wrappers, buffer);
        Capacity = capacity;

        return result;
    }
}
