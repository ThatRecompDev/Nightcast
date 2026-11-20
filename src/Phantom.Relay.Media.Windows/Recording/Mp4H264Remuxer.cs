// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows.Recording;

/// <summary>
/// An H.264 stream written into an MP4 file as it is, without re-encoding: Media Foundation's sink writer with the
/// stream's type as both its input and its output type passes each access unit (Annex-B, as the stream carries it)
/// through to the MPEG-4 file sink. The stream starts at a key frame, whose parameter sets describe it
/// (MF_MT_MPEG_SEQUENCE_HEADER); each unit then goes in with its time and duration (100 ns units) and its key-frame mark.
/// One thread uses it; game-agnostic (an engine candidate, beside an encoder of its own).
/// </summary>
public sealed unsafe class Mp4H264Remuxer : IDisposable
{
    // The sequence parameter set's profile_idc and level_idc, after its NAL header byte (H.264 7.3.2.1.1).
    private const int ProfileOffset = 1;
    private const int LevelOffset = 3;

    private static readonly byte[] StartCode = [0, 0, 0, 1];

    private readonly ComWrappers _wrappers = new StrategyBasedComWrappers();
    private nint _writer;
    private IMFSinkWriter? _writerApi;
    private uint _stream;
    private bool _started;
    private bool _disposed;

    private Mp4H264Remuxer()
    {
    }

    /// <summary>The access units written so far.</summary>
    public long Written { get; private set; }

    /// <summary>
    /// A file at <paramref name="path"/> for a stream of <paramref name="width"/> x <paramref name="height"/> at a nominal
    /// <paramref name="framesPerSecond"/>, described by the parameter sets of <paramref name="keyUnit"/> (its first unit,
    /// a key frame, which is not written yet); null and why when Media Foundation refuses.
    /// </summary>
    public static Mp4H264Remuxer? TryCreate(string path, int width, int height, int framesPerSecond, ReadOnlySpan<byte> keyUnit, out string? problem)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!AnnexBUnits.TryFind(keyUnit, AnnexBUnits.SequenceParameterSetType, out var sequence)
            || !AnnexBUnits.TryFind(keyUnit, AnnexBUnits.PictureParameterSetType, out var picture)
            || sequence.Length <= LevelOffset)
        {
            problem = "the first key frame carries no sequence and picture parameter sets";

            return null;
        }

        var remuxer = new Mp4H264Remuxer();
        problem = remuxer.Open(path, width, height, framesPerSecond, sequence, picture);

        if (problem is not null)
        {
            remuxer.Dispose();

            return null;
        }

        return remuxer;
    }

    /// <summary>Writes one access unit at <paramref name="time"/> for <paramref name="duration"/> (100 ns units); false when the sink refused it.</summary>
    public bool TryWrite(ReadOnlySpan<byte> accessUnit, long time, long duration, bool key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var sample = MediaFoundationSampleBuffer.TryCreate(_wrappers, accessUnit.Length, out _);

        if (sample is null || ComObjects.Failed(sample.TryWrite(accessUnit, time)))
        {
            return false;
        }

        var cleanPoint = key ? 1u : 0u;
        var api = ComObjects.Wrap<IMFSample>(_wrappers, sample.Sample);
        var result = RecordingTypes.Next(api.SetSampleDuration(duration), api.SetUInt32(in MediaFoundationIds.CleanPoint, cleanPoint));
        ComObjects.Free(api);

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.WriteSample(_stream, sample.Sample);
        }

        if (ComObjects.Failed(result))
        {
            return false;
        }

        Written++;

        return true;
    }

    /// <summary>Finishes the file (its index written); false and why when the sink could not.</summary>
    public bool TryFinish(out string? problem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        problem = null;
        var result = _writerApi!.FinalizeWriting();

        if (ComObjects.Failed(result))
        {
            problem = $"the MP4 file could not be finished (0x{result:X8})";
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
        ComObjects.Free(_writerApi);
        ComObjects.Release(ref _writer);

        if (_started)
        {
            _ = NativeMethods.MFShutdown();
        }
    }

    // The sink writer, the stream's type as both its type and its input type, then writing; why not, or null.
    private string? Open(string path, int width, int height, int framesPerSecond, ReadOnlySpan<byte> sequence, ReadOnlySpan<byte> picture)
    {
        var result = MediaFoundationTransforms.Start();
        _started = !ComObjects.Failed(result);

        if (_started)
        {
            result = NativeMethods.MFCreateSinkWriterFromURL(path, 0, 0, out _writer);
        }

        if (ComObjects.Failed(result))
        {
            return $"Media Foundation's sink writer could not open the file (0x{result:X8})";
        }

        _writerApi = ComObjects.Wrap<IMFSinkWriter>(_wrappers, _writer);
        result = RecordingTypes.TryCreate(_wrappers, in MediaFoundationIds.H264Subtype, width, height, framesPerSecond, out var type, out var attributes);

        if (!ComObjects.Failed(result) && attributes is not null)
        {
            result = Begin(type, attributes, sequence, picture);
        }

        ComObjects.Free(attributes);
        ComObjects.Release(ref type);

        if (ComObjects.Failed(result))
        {
            return $"the MP4 sink refused the H.264 stream (0x{result:X8})";
        }

        return null;
    }

    // The type described, added as the stream's and as its input (the same: passed through), and writing begun.
    private int Begin(nint type, IMFAttributes attributes, ReadOnlySpan<byte> sequence, ReadOnlySpan<byte> picture)
    {
        var result = Describe(attributes, sequence, picture);

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.AddStream(type, out _stream);
        }

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.SetInputMediaType(_stream, type, 0);
        }

        if (!ComObjects.Failed(result))
        {
            result = _writerApi!.BeginWriting();
        }

        return result;
    }

    // The stream's parameter sets (Annex-B, each after a start code), profile and level, and its colour.
    private static int Describe(IMFAttributes type, ReadOnlySpan<byte> sequence, ReadOnlySpan<byte> picture)
    {
        byte[] header = [.. StartCode, .. sequence, .. StartCode, .. picture];
        int result;

        fixed (byte* bytes = header)
        {
            result = type.SetBlob(in MediaFoundationIds.MpegSequenceHeader, bytes, (uint)header.Length);
        }

        result = RecordingTypes.Next(result, type.SetUInt32(in MediaFoundationIds.Mpeg2Profile, sequence[ProfileOffset]));
        result = RecordingTypes.Next(result, type.SetUInt32(in MediaFoundationIds.Mpeg2Level, sequence[LevelOffset]));

        return RecordingTypes.Next(result, RecordingTypes.DescribeBt709(type));
    }
}
