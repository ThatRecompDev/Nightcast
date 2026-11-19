// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Audio;

/// <summary>
/// The Opus decoder the player drives: the engine's (<see cref="EngineOpusFrameDecoder"/>) in production, a fake in
/// tests, since the engine has no encoder to make real packets with. Every output is interleaved stereo float, full
/// scale 1.0, with room for <see cref="OpusStreamFormat.MaxPacketSamples"/> samples a channel. Only
/// <see cref="PacketSamples"/> is called from the network thread; the rest run on the audio thread.
/// </summary>
public interface IOpusFrameDecoder
{
    /// <summary>
    /// The samples a channel <paramref name="packet"/> holds, read from its TOC byte and framing, or 0 when it is
    /// malformed. Called on the network thread, so it reads no decoder state.
    /// </summary>
    int PacketSamples(ReadOnlySpan<byte> packet);

    /// <summary>
    /// Decodes <paramref name="packet"/>; returns the samples a channel written, negative when the packet was
    /// malformed and concealed instead.
    /// </summary>
    int Decode(ReadOnlySpan<byte> packet, Span<float> output);

    /// <summary>
    /// Rebuilds the <paramref name="samples"/> lost just before <paramref name="nextPacket"/> from its in-band FEC
    /// (SILK's LBRR frames). Returns the samples a channel written, or 0 when the packet carries none or the decoder
    /// cannot use it; the player then conceals.
    /// </summary>
    int DecodeFec(ReadOnlySpan<byte> nextPacket, int samples, Span<float> output);

    /// <summary>Conceals <paramref name="samples"/> lost samples a channel; returns how many it wrote.</summary>
    int DecodeLost(int samples, Span<float> output);
}
