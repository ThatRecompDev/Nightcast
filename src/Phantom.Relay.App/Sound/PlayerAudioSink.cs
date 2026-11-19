// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Audio;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.App.Sound;

/// <summary>The stream's sound into the player: each Opus payload from the network thread into its jitter buffer.</summary>
public sealed class PlayerAudioSink : IStreamAudioSink
{
    private readonly OpusStreamPlayer _player;

    public PlayerAudioSink(OpusStreamPlayer player)
    {
        _player = player;
    }

    [HotPath]
    public void Push(ReadOnlySpan<byte> payload, ushort sequence, uint timestamp)
    {
        _player.Push(payload, sequence, timestamp);
    }
}
