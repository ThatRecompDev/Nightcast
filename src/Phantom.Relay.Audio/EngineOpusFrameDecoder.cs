// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Audio.Codecs.Opus;
using Phantom.Engine.Core;

namespace Phantom.Relay.Audio;

/// <summary>
/// The engine's Opus decoder (SILK, CELT and hybrid, conformant to RFC 6716 and 8251) behind the player's port. Its
/// concealment is the engine's <see cref="OpusDecoder.DecodeLost"/>. It has no FEC yet: the engine reads the LBRR
/// frames past without decoding them, so <see cref="DecodeFec"/> answers 0 and a lost packet is concealed.
/// </summary>
public sealed class EngineOpusFrameDecoder : IOpusFrameDecoder
{
    private readonly OpusDecoder _decoder;

    /// <param name="decoder">A decoder of <see cref="OpusStreamFormat.Channels"/> channels, used by nothing else.</param>
    public EngineOpusFrameDecoder(OpusDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);

        if (decoder.Channels != OpusStreamFormat.Channels)
        {
            throw new ArgumentException("the stream decodes to stereo", nameof(decoder));
        }

        _decoder = decoder;
    }

    [HotPath]
    public int PacketSamples(ReadOnlySpan<byte> packet)
    {
        return OpusDecoder.SamplesOf(packet);
    }

    [HotPath]
    public int Decode(ReadOnlySpan<byte> packet, Span<float> output)
    {
        return _decoder.Decode(packet, output);
    }

    [HotPath]
    public int DecodeFec(ReadOnlySpan<byte> nextPacket, int samples, Span<float> output)
    {
        return 0;
    }

    [HotPath]
    public int DecodeLost(int samples, Span<float> output)
    {
        return _decoder.DecodeLost(samples, output);
    }
}
