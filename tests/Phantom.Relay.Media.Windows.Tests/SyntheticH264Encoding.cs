// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows.Tests;

/// <summary>
/// An H.264 stream made at test time, since captures of a real session never enter the repository: Windows' own
/// software encoder (CLSID_CMSH264EncoderMFT), Main profile in low-latency mode (no B-frames, as the console's), fed a
/// moving gradient. Each access unit comes out in Annex-B form, the first with its parameter sets.
/// </summary>
public static unsafe class SyntheticH264Encoding
{
    public const long FrameUnits = 166_667;

    /// <summary>The luma the gradient has at a point of a frame.</summary>
    public static byte LumaAt(int x, int y, int frame)
    {
        return (byte)((x / 4) + (y / 4) + (frame * 8));
    }

    public static List<byte[]> Encode(int width, int height, int frames)
    {
        Check(MediaFoundationTransforms.Start());
        var wrappers = new StrategyBasedComWrappers();
        Check(MediaFoundationTransforms.TryCreate(wrappers, in MediaFoundationIds.H264Encoder, out var transform, out var api));

        try
        {
            Configure(wrappers, transform, api!, width, height);

            var units = new List<byte[]>();
            var picture = new byte[width * height * 3 / 2];

            for (var frame = 0; frame < frames; frame++)
            {
                Paint(picture, width, height, frame);
                Feed(wrappers, api!, picture, frame);
                Collect(wrappers, api!, units);
            }

            Check(api!.ProcessMessage(MediaFoundationValues.CommandDrain, 0));
            Collect(wrappers, api, units);
            PrependParameterSets(wrappers, api, units);

            return units;
        }
        finally
        {
            ComObjects.Free(api);
            ComObjects.Release(ref transform);
            _ = NativeMethods.MFShutdown();
        }
    }

    private static void Configure(ComWrappers wrappers, nint transform, IMFTransform api, int width, int height)
    {
        // Low latency, so no B-frames: the encoder takes the setting as a VT_BOOL, where the decoder wants a VT_UI4.
        Check(MediaFoundationTransforms.SetCodecValue(wrappers, transform, in MediaFoundationIds.LowLatency, ComVariant.Create(true)));

        // The output type first, as an encoder requires: H.264 Main at 20 Mbit/s, 60 fps.
        Check(MediaFoundationTypes.TryCreateVideo(wrappers, in MediaFoundationIds.H264Subtype, out var output, out var outputAttributes));
        Check(outputAttributes!.SetUInt32(in MediaFoundationIds.AverageBitrate, 20_000_000));
        Check(outputAttributes.SetUInt32(in MediaFoundationIds.Mpeg2Profile, 77));
        Describe(outputAttributes, width, height);

        Check(api.SetOutputType(0, output, 0));
        ComObjects.Free(outputAttributes);
        ComObjects.Release(ref output);

        Check(MediaFoundationTypes.TryCreateVideo(wrappers, in MediaFoundationIds.Nv12Subtype, out var input, out var inputAttributes));
        Describe(inputAttributes!, width, height);
        Check(api.SetInputType(0, input, 0));
        ComObjects.Free(inputAttributes);
        ComObjects.Release(ref input);

        Check(api.ProcessMessage(MediaFoundationValues.NotifyBeginStreaming, 0));
        Check(api.ProcessMessage(MediaFoundationValues.NotifyStartOfStream, 0));
    }

    private static void Describe(IMFAttributes type, int width, int height)
    {
        Check(type.SetUInt64(in MediaFoundationIds.FrameSize, MediaFoundationTypes.Pack((uint)width, (uint)height)));
        Check(type.SetUInt64(in MediaFoundationIds.FrameRate, MediaFoundationTypes.Pack(60, 1)));
        Check(type.SetUInt64(in MediaFoundationIds.PixelAspectRatio, MediaFoundationTypes.Pack(1, 1)));
        Check(type.SetUInt32(in MediaFoundationIds.InterlaceMode, MediaFoundationValues.Progressive));
    }

    private static void Paint(byte[] picture, int width, int height, int frame)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                picture[(y * width) + x] = LumaAt(x, y, frame);
            }
        }

        var chroma = picture.AsSpan(width * height);

        for (var index = 0; index < chroma.Length; index += 2)
        {
            chroma[index] = (byte)(96 + ((index / 2) % 64));
            chroma[index + 1] = (byte)(160 - ((index / 2) % 64));
        }
    }

    private static void Feed(ComWrappers wrappers, IMFTransform api, byte[] picture, int frame)
    {
        using var input = MediaFoundationSampleBuffer.TryCreate(wrappers, picture.Length, out var problem);
        Assert.True(input is not null, problem);

        Check(input.TryWrite(picture, frame * FrameUnits));

        // The encoder wants each picture's duration as well as its time.
        var sample = ComObjects.Wrap<IMFSample>(wrappers, input.Sample);
        Check(sample.SetSampleDuration(FrameUnits));
        ComObjects.Free(sample);

        Check(api.ProcessInput(0, input.Sample, 0));
    }

    // Every access unit the encoder has ready, into our own output sample (the software encoder provides none).
    private static void Collect(ComWrappers wrappers, IMFTransform api, List<byte[]> units)
    {
        while (true)
        {
            Check(api.GetOutputStreamInfo(0, out var info));
            using var output = MediaFoundationSampleBuffer.TryCreate(wrappers, (int)info.Size, out var problem);
            Assert.True(output is not null, problem);

            var buffer = new MftOutputDataBuffer { Sample = output.Sample };
            var result = api.ProcessOutput(0, 1, &buffer, out _);
            ComObjects.Release(ref buffer.Events);

            if (result == MediaFoundationValues.NeedMoreInput)
            {
                return;
            }

            Check(result);
            units.Add(Read(wrappers, output.Sample));
        }
    }

    private static byte[] Read(ComWrappers wrappers, nint sample)
    {
        var sampleApi = ComObjects.Wrap<IMFSample>(wrappers, sample);
        Check(sampleApi.GetBufferByIndex(0, out var buffer));
        ComObjects.Free(sampleApi);

        var bufferApi = ComObjects.Wrap<IMFMediaBuffer>(wrappers, buffer);
        Check(bufferApi.Lock(out var data, out _, out var length));
        var bytes = new ReadOnlySpan<byte>((void*)data, (int)length).ToArray();
        Check(bufferApi.Unlock());
        ComObjects.Free(bufferApi);
        ComObjects.Release(ref buffer);

        return bytes;
    }

    // The encoder may keep the parameter sets in its output type only: put them before the first key frame.
    private static void PrependParameterSets(ComWrappers wrappers, IMFTransform api, List<byte[]> units)
    {
        if (units.Count == 0 || AnnexBUnits.TryFind(units[0], AnnexBUnits.SequenceParameterSetType, out _))
        {
            return;
        }

        Check(api.GetOutputCurrentType(0, out var type));
        var attributes = ComObjects.Wrap<IMFAttributes>(wrappers, type);
        Check(attributes.GetBlobSize(in MediaFoundationIds.MpegSequenceHeader, out var size));
        var sets = new byte[size];

        fixed (byte* target = sets)
        {
            Check(attributes.GetBlob(in MediaFoundationIds.MpegSequenceHeader, target, size, out _));
        }

        ComObjects.Free(attributes);
        ComObjects.Release(ref type);
        units[0] = [.. sets, .. units[0]];
    }

    private static void Check(int result)
    {
        Assert.True(result >= 0, $"Media Foundation failed (0x{result:X8})");
    }
}
