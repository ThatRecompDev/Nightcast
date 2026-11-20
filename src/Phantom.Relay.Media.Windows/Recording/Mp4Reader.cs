// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows.Recording;

/// <summary>
/// Reads an MP4 file's video back through Media Foundation's source reader, as stored (no decoding): every sample
/// counted, its key frames marked, the size, the length and the codec (<see cref="TryRead"/>). A recording checks its
/// own files with it. Game-agnostic.
/// </summary>
public static class Mp4Reader
{
    private const string H264Name = "H.264";
    private const string HevcName = "HEVC";
    private const string OtherName = "another codec";

    /// <summary>The facts of the file at <paramref name="path"/>'s first video stream; null and why when it cannot be read.</summary>
    public static Mp4Facts? TryRead(string path, out string? problem)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var result = MediaFoundationTransforms.Start();

        if (ComObjects.Failed(result))
        {
            problem = $"MFStartup failed (0x{result:X8})";

            return null;
        }

        try
        {
            return Read(path, out problem);
        }
        finally
        {
            _ = NativeMethods.MFShutdown();
        }
    }

    private static Mp4Facts? Read(string path, out string? problem)
    {
        var wrappers = new StrategyBasedComWrappers();
        var result = NativeMethods.MFCreateSourceReaderFromURL(path, 0, out var reader);

        if (ComObjects.Failed(result))
        {
            problem = $"the file could not be opened (0x{result:X8})";

            return null;
        }

        var api = ComObjects.Wrap<IMFSourceReader>(wrappers, reader);

        try
        {
            return Describe(wrappers, api, new FileInfo(path).Length, out problem);
        }
        finally
        {
            ComObjects.Free(api);
            ComObjects.Release(ref reader);
        }
    }

    // The first video stream alone: its stored type, the presentation's length, then every sample counted.
    private static unsafe Mp4Facts? Describe(ComWrappers wrappers, IMFSourceReader reader, long bytes, out string? problem)
    {
        problem = null;
        _ = reader.SetStreamSelection(MediaFoundationValues.AllStreams, 0);

        if (ComObjects.Failed(reader.SetStreamSelection(MediaFoundationValues.FirstVideoStream, 1))
            || !TryReadType(wrappers, reader, out var width, out var height, out var codec))
        {
            problem = "the file holds no video stream";

            return null;
        }

        var duration = default(ComVariant);
        var length = TimeSpan.Zero;

        if (!ComObjects.Failed(reader.GetPresentationAttribute(MediaFoundationValues.MediaSource, in MediaFoundationIds.PresentationDuration, &duration)))
        {
            length = TimeSpan.FromTicks((long)duration.As<ulong>());
        }

        duration.Dispose();
        var (frames, keys) = CountSamples(wrappers, reader);

        return new Mp4Facts(frames, keys, width, height, length, codec, bytes);
    }

    private static bool TryReadType(ComWrappers wrappers, IMFSourceReader reader, out int width, out int height, out string codec)
    {
        width = 0;
        height = 0;
        codec = OtherName;

        if (ComObjects.Failed(reader.GetNativeMediaType(MediaFoundationValues.FirstVideoStream, 0, out var type)))
        {
            return false;
        }

        var attributes = ComObjects.Wrap<IMFAttributes>(wrappers, type);

        if (MediaFoundationTypes.TryReadNv12(attributes, out var format))
        {
            (width, height) = (format.Width, format.Height);
        }

        codec = CodecOf(attributes);
        ComObjects.Free(attributes);
        ComObjects.Release(ref type);

        return true;
    }

    private static string CodecOf(IMFAttributes type)
    {
        if (MediaFoundationTypes.IsSubtype(type, in MediaFoundationIds.H264Subtype))
        {
            return H264Name;
        }

        if (MediaFoundationTypes.IsSubtype(type, in MediaFoundationIds.HevcSubtype))
        {
            return HevcName;
        }

        return OtherName;
    }

    // Every sample to the end of the stream, and how many of them are key frames.
    private static (int Frames, int Keys) CountSamples(ComWrappers wrappers, IMFSourceReader reader)
    {
        var frames = 0;
        var keys = 0;

        while (!ComObjects.Failed(reader.ReadSample(MediaFoundationValues.FirstVideoStream, 0, out _, out var flags, out _, out var sample)))
        {
            if (sample != 0)
            {
                frames++;
                keys += IsKey(wrappers, sample);
                ComObjects.Release(ref sample);
            }

            if ((flags & MediaFoundationValues.EndOfStream) != 0)
            {
                break;
            }
        }

        return (frames, keys);
    }

    private static int IsKey(ComWrappers wrappers, nint sample)
    {
        var api = ComObjects.Wrap<IMFSample>(wrappers, sample);
        var key = !ComObjects.Failed(api.GetUInt32(in MediaFoundationIds.CleanPoint, out var clean)) && clean != 0;
        ComObjects.Free(api);

        if (key)
        {
            return 1;
        }

        return 0;
    }
}
