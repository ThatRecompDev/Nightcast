// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Audio.Codecs.Opus;

namespace Phantom.Relay.Audio;

/// <summary>
/// The console's game sound as it reaches the player: Opus at 48 kHz in two channels, RTP payload type 111 (the
/// SDP's "opus/48000/2" with minptime=10 and useinbandfec=1), already decrypted by the media layer.
/// </summary>
public static class OpusStreamFormat
{
    /// <summary>The RTP clock and the decoded rate: Opus over RTP always counts at 48 kHz (RFC 7587).</summary>
    public const int SampleRate = OpusDecoder.SampleRate;

    /// <summary>The decoded channels: stereo, whatever each packet codes.</summary>
    public const int Channels = 2;

    /// <summary>The payload type the console's offer gives Opus.</summary>
    public const int PayloadType = 111;

    /// <summary>The most samples a channel one packet holds: 120 ms.</summary>
    public const int MaxPacketSamples = OpusDecoder.MaxPacketSamples;

    /// <summary>The largest payload kept: an Ethernet MTU. The console's 10 ms packets are far smaller.</summary>
    public const int MaxPayloadBytes = 1500;

    /// <summary>Samples a channel in one millisecond at 48 kHz.</summary>
    public const int SamplesPerMillisecond = SampleRate / MillisecondsPerSecond;

    private const int MillisecondsPerSecond = 1000;
}
