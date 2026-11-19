// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Audio.Codecs.Opus;
using Phantom.Engine.Testing.Time;

namespace Phantom.Relay.Audio.Tests;

/// <summary>
/// The engine's decoder behind the port, on hand-made packets: a TOC byte (CELT-only fullband, stereo, one frame)
/// and the two-byte CELT frame that decodes as silence (the one the engine itself uses to fade its overlap out).
/// </summary>
public sealed class EngineOpusFrameDecoderTests
{
    /// <summary>Configuration 30 (CELT FB 10 ms), stereo, code 0.</summary>
    private static readonly byte[] TenMilliseconds = [0xF4, 0xFF, 0xFF];

    /// <summary>Configuration 31 (CELT FB 20 ms), stereo, code 0.</summary>
    private static readonly byte[] TwentyMilliseconds = [0xFC, 0xFF, 0xFF];

    [Fact]
    public void ReadsTheDurationFromTheToc()
    {
        var decoder = new EngineOpusFrameDecoder(new OpusDecoder(OpusStreamFormat.Channels));

        Assert.Equal(480, decoder.PacketSamples(TenMilliseconds));
        Assert.Equal(960, decoder.PacketSamples(TwentyMilliseconds));
        Assert.Equal(0, decoder.PacketSamples([]));
    }

    [Fact]
    public void DecodesASilentCeltPacket()
    {
        var decoder = new EngineOpusFrameDecoder(new OpusDecoder(OpusStreamFormat.Channels));
        var output = new float[OpusStreamFormat.MaxPacketSamples * OpusStreamFormat.Channels];
        output.AsSpan().Fill(1f);

        var decoded = decoder.Decode(TwentyMilliseconds, output);

        Assert.Equal(960, decoded);
        Assert.All(output.Take(decoded * OpusStreamFormat.Channels), sample => Assert.Equal(0f, sample, 6));
    }

    [Fact]
    public void Rebuilds_a_lost_frame_through_the_engines_fec_and_conceals_without_a_next_packet()
    {
        var decoder = new EngineOpusFrameDecoder(new OpusDecoder(OpusStreamFormat.Channels));
        var output = new float[OpusStreamFormat.MaxPacketSamples * OpusStreamFormat.Channels];
        decoder.Decode(TenMilliseconds, output);

        var fec = decoder.DecodeFec(TenMilliseconds, 480, output);
        var concealed = decoder.DecodeLost(480, output);

        Assert.Equal(480, fec);
        Assert.Equal(480, concealed);
    }

    [Fact]
    public void RefusesAMonoDecoder()
    {
        Assert.Throws<ArgumentException>(() => new EngineOpusFrameDecoder(new OpusDecoder(1)));
    }

    [Fact]
    public void PlaysTheStreamThroughTheEnginesDecoder()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var decoder = new EngineOpusFrameDecoder(new OpusDecoder(OpusStreamFormat.Channels));
        var player = new OpusStreamPlayer(decoder, clock, OpusStreamFormat.SampleRate, PlayoutSettings.Default);
        var output = new float[480 * OpusStreamFormat.Channels];

        for (var index = 0; index < 20; index++)
        {
            player.Push(TenMilliseconds, (ushort)index, (uint)(index * 480));
            player.Render(output, 480);
            clock.AdvanceMilliseconds(10);
        }

        Assert.True(player.Stats.PacketsPlayed > 10);
        Assert.Equal(0, player.Stats.RefusedPackets);
        Assert.All(output, sample => Assert.Equal(0f, sample, 6));
    }
}
