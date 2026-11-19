// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Stream.Tests.Fakes;

/// <summary>
/// A decoder whose every unit gives a picture, handed on on the GPU while it shares and read back otherwise; it notes
/// the order of the worker's calls.
/// </summary>
public sealed class FakeGpuDecoder : IVideoDecoder, IGpuPictureProducer
{
    public string CodecId => "V_TEST";

    public string LastError => string.Empty;

    public bool IsSharing { get; set; }

    public List<string> Calls { get; } = [];

    public List<long> PublishedAt { get; } = [];

    public List<PictureKind> Kinds { get; } = [];

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha)
    {
        Calls.Add("decode");

        return VideoDecodeResult.Picture;
    }

    public bool TakePicture(VideoFrame target)
    {
        Calls.Add("take");

        return false;
    }

    public void Prepare()
    {
        Calls.Add("prepare");
    }

    public bool Publish(PictureKind kind, long decodedAt, out int width, out int height)
    {
        Calls.Add("publish");
        PublishedAt.Add(decodedAt);
        Kinds.Add(kind);
        width = 2560;
        height = 1440;

        return true;
    }

    public void Flush()
    {
    }

    public void Dispose()
    {
    }
}
