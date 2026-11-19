// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Where the console's sound goes: each Opus RTP payload, decrypted, with its RTP sequence number and timestamp (48 kHz),
/// in arrival order (not reordered: the player's jitter buffer orders them). Called on the stream's network thread, so
/// an implementation copies what it keeps and returns at once; the span is valid only during the call.
/// </summary>
public interface IStreamAudioSink
{
    void Push(ReadOnlySpan<byte> payload, ushort sequence, uint timestamp);
}
