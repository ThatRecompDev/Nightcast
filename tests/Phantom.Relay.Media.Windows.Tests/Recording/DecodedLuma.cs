// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows.Tests.Recording;

/// <summary>
/// An MP4 file's video decoded back to NV12 by Media Foundation's source reader, each frame's luma at the picture's
/// centre in order: what an encoded file shows, frame by frame.
/// </summary>
public static unsafe class DecodedLuma
{
    public static List<byte> AtCentre(string path)
    {
        Check(MediaFoundationTransforms.Start());
        var wrappers = new StrategyBasedComWrappers();
        Check(NativeMethods.MFCreateSourceReaderFromURL(path, 0, out var reader));
        var api = ComObjects.Wrap<IMFSourceReader>(wrappers, reader);

        try
        {
            var format = DecodeToNv12(wrappers, api);

            return ReadAll(wrappers, api, format);
        }
        finally
        {
            ComObjects.Free(api);
            ComObjects.Release(ref reader);
            _ = NativeMethods.MFShutdown();
        }
    }

    private static Nv12Format DecodeToNv12(StrategyBasedComWrappers wrappers, IMFSourceReader reader)
    {
        Check(MediaFoundationTypes.TryCreateVideo(wrappers, in MediaFoundationIds.Nv12Subtype, out var type, out var attributes));
        Check(reader.SetCurrentMediaType(MediaFoundationValues.FirstVideoStream, 0, type));
        ComObjects.Free(attributes);
        ComObjects.Release(ref type);

        Check(reader.GetCurrentMediaType(MediaFoundationValues.FirstVideoStream, out var current));
        var currentAttributes = ComObjects.Wrap<IMFAttributes>(wrappers, current);
        Assert.True(MediaFoundationTypes.TryReadNv12(currentAttributes, out var format));
        ComObjects.Free(currentAttributes);
        ComObjects.Release(ref current);

        return format;
    }

    private static List<byte> ReadAll(StrategyBasedComWrappers wrappers, IMFSourceReader reader, Nv12Format format)
    {
        var lumas = new List<byte>();

        while (true)
        {
            Check(reader.ReadSample(MediaFoundationValues.FirstVideoStream, 0, out _, out var flags, out _, out var sample));

            if (sample != 0)
            {
                lumas.Add(Centre(wrappers, sample, format));
                ComObjects.Release(ref sample);
            }

            if ((flags & MediaFoundationValues.EndOfStream) != 0)
            {
                return lumas;
            }
        }
    }

    private static byte Centre(StrategyBasedComWrappers wrappers, nint sample, Nv12Format format)
    {
        var sampleApi = ComObjects.Wrap<IMFSample>(wrappers, sample);
        Check(sampleApi.GetBufferByIndex(0, out var buffer));
        ComObjects.Free(sampleApi);

        var bufferApi = ComObjects.Wrap<IMFMediaBuffer>(wrappers, buffer);
        Check(bufferApi.Lock(out var data, out _, out _));
        var luma = ((byte*)data)[(format.Height / 2 * format.Stride) + (format.Width / 2)];
        Check(bufferApi.Unlock());
        ComObjects.Free(bufferApi);
        ComObjects.Release(ref buffer);

        return luma;
    }

    private static void Check(int result)
    {
        Assert.True(result >= 0, $"Media Foundation failed (0x{result:X8})");
    }
}
