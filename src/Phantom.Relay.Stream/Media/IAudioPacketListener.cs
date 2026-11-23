// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Sees each of the console's Opus packets as the network thread hands it to the sound (<see cref="AudioPacketTap"/>):
/// the payload, valid only during the call, and its RTP timestamp (48 kHz). It copies what it keeps and returns at once.
/// </summary>
public interface IAudioPacketListener
{
    void Received(ReadOnlySpan<byte> payload, uint timestamp);
}
