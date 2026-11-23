// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Audio.Codecs.Opus;
using Phantom.Engine.Core;

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// What the writer reads of an Opus packet: its length in 48 kHz samples (from its TOC byte and framing, the engine's
/// <see cref="OpusDecoder.SamplesOf"/>), and the packet that stands for a lost one: its TOC alone with frame count code 0,
/// one frame of zero bytes, which a decoder conceals (RFC 6716 3.2.1: a zero-length frame is a lost or DTX frame).
/// </summary>
internal static class OpusPackets
{
    /// <summary>The TOC's frame count code: its low two bits (RFC 6716 3.1).</summary>
    public const int FrameCountMask = 0x03;

    /// <summary>The samples <paramref name="packet"/> holds a channel, or 0 when it is not a valid packet.</summary>
    [HotPath]
    public static int Samples(ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty)
        {
            return 0;
        }

        return OpusDecoder.SamplesOf(packet);
    }

    /// <summary>The one-byte packet that conceals one frame of the configuration <paramref name="packet"/> has.</summary>
    [HotPath]
    public static byte ConcealmentOf(ReadOnlySpan<byte> packet)
    {
        return (byte)(packet[0] & ~FrameCountMask);
    }
}
