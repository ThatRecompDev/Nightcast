// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests.Media;

/// <summary>A sound sink and listener that keeps the timestamps it saw.</summary>
public sealed class CountingSink : IStreamAudioSink, IAudioPacketListener
{
    public List<uint> Timestamps { get; } = [];

    public void Push(ReadOnlySpan<byte> payload, ushort sequence, uint timestamp)
    {
        Timestamps.Add(timestamp);
    }

    public void Received(ReadOnlySpan<byte> payload, uint timestamp)
    {
        Timestamps.Add(timestamp);
    }
}