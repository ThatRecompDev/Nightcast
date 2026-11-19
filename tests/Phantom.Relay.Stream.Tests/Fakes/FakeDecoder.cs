// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Stream.Tests.Fakes;

/// <summary>A decoder that takes every unit and shows nothing; it counts the units and whether it was disposed.</summary>
public sealed class FakeDecoder : IVideoDecoder
{
    public string CodecId => "V_TEST";

    public string LastError => string.Empty;

    public int Units { get; private set; }

    public bool Disposed { get; private set; }

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha)
    {
        Units++;

        return VideoDecodeResult.Hidden;
    }

    public bool TakePicture(VideoFrame target)
    {
        return false;
    }

    public void Flush()
    {
    }

    public void Dispose()
    {
        Disposed = true;
    }
}
