// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>Drops the sound: for a head without a player yet, and for the probe, which only counts packets.</summary>
public sealed class SilentAudioSink : IStreamAudioSink
{
    public void Push(ReadOnlySpan<byte> payload, ushort sequence, uint timestamp)
    {
    }
}
