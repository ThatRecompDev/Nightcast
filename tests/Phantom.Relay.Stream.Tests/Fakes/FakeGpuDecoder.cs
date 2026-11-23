// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.Stream.Tests.Fakes;

/// <summary>
/// A decoder whose every unit gives a picture, handed on on the GPU while it shares and read back otherwise (a 2x2
/// grey picture when <see cref="GivesPictures"/>); it notes the order of the worker's calls.
/// </summary>
public sealed class FakeGpuDecoder : IVideoDecoder, IGpuPictureProducer
{
    private const int Side = 2;
    private const byte Grey = 128;

    private bool _waiting;

    public string CodecId => "V_TEST";

    public string LastError => string.Empty;

    public bool IsSharing { get; set; }

    /// <summary>Whether a read-back gives a picture (once a decode); false: none comes.</summary>
    public bool GivesPictures { get; set; }

    public List<string> Calls { get; } = [];

    public List<long> PublishedAt { get; } = [];

    public List<PictureKind> Kinds { get; } = [];

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha)
    {
        Calls.Add("decode");
        _waiting = GivesPictures;

        return VideoDecodeResult.Picture;
    }

    public bool TakePicture(VideoFrame target)
    {
        Calls.Add("take");

        if (!_waiting)
        {
            return false;
        }

        _waiting = false;
        Fill(target);

        return true;
    }

    public void Prepare()
    {
        Calls.Add("prepare");
    }

    public List<uint> RtpTimestamps { get; } = [];

    public bool Publish(PictureKind kind, long decodedAt, uint rtpTimestamp, out int width, out int height)
    {
        Calls.Add("publish");
        PublishedAt.Add(decodedAt);
        RtpTimestamps.Add(rtpTimestamp);
        Kinds.Add(kind);
        width = 2560;
        height = 1440;

        return true;
    }

    public bool TryReadBack(VideoFrame target)
    {
        Calls.Add("read back");
        Fill(target);

        return true;
    }

    public void Flush()
    {
    }

    public void Dispose()
    {
    }

    private static void Fill(VideoFrame target)
    {
        target.Prepare(Side, Side, 1, 1, alpha: false);
        target.Luma.Fill(Grey);
        target.Cb.Fill(Grey);
        target.Cr.Fill(Grey);
    }
}
