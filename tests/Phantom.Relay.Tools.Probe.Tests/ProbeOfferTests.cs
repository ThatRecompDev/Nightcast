// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;

namespace Phantom.Relay.Tools.Probe.Tests;

public sealed class ProbeOfferTests
{
    [Fact]
    public void The_offer_bundles_sound_video_and_data_with_crlf_lines()
    {
        string offer = ProbeOffer.Create(new SeededRandomSource(7));

        var lines = offer.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("v=0", lines[0]);
        Assert.Contains("a=group:BUNDLE 0 1 2", lines);
        Assert.Contains("m=audio 9 UDP/TLS/RTP/SAVPF 111", lines);
        Assert.Contains("m=video 9 UDP/TLS/RTP/SAVPF 102 104 106 45", lines);
        Assert.Contains("m=application 9 UDP/DTLS/SCTP webrtc-datachannel", lines);
        Assert.DoesNotContain('\n', offer.Replace("\r\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Video_is_offered_best_first_up_to_level_5_1_and_received_only()
    {
        string offer = ProbeOffer.Create(new SeededRandomSource(7));

        Assert.Contains("a=fmtp:102 level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=640033", offer, StringComparison.Ordinal);
        Assert.Contains("a=rtpmap:45 AV1/90000", offer, StringComparison.Ordinal);
        Assert.Contains("a=mid:1\r\na=recvonly", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fingerprint_has_32_bytes_and_every_m_line_shares_one_ice_identity()
    {
        string offer = ProbeOffer.Create(new SeededRandomSource(7));
        var lines = offer.Split("\r\n");

        string fingerprint = lines.First(static line => line.StartsWith("a=fingerprint:sha-256 ", StringComparison.Ordinal));
        var ufrags = lines.Where(static line => line.StartsWith("a=ice-ufrag:", StringComparison.Ordinal)).Distinct().ToList();

        Assert.Equal(32, fingerprint["a=fingerprint:sha-256 ".Length..].Split(':').Length);
        Assert.Single(ufrags);
        Assert.Equal(3, lines.Count(static line => line.StartsWith("a=ice-ufrag:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Each_offer_has_fresh_credentials()
    {
        var random = new SeededRandomSource(7);

        Assert.NotEqual(ProbeOffer.Create(random), ProbeOffer.Create(random));
    }

    [Fact]
    public void The_answer_summary_keeps_codecs_and_drops_credentials_and_candidates()
    {
        string answer = "v=0\r\nm=video 9 UDP/TLS/RTP/SAVPF 102\r\nb=AS:30000\r\na=ice-pwd:secret\r\na=candidate:1 1 udp 1 10.0.0.2 9 typ host\r\n"
            + "a=fingerprint:sha-256 AA\r\na=rtpmap:102 H264/90000\r\na=fmtp:102 profile-level-id=640033\r\na=sendonly\r\n";

        var summary = AnswerSummary.Of(answer);

        Assert.Equal(["m=video 9 UDP/TLS/RTP/SAVPF 102", "b=AS:30000", "a=rtpmap:102 H264/90000", "a=fmtp:102 profile-level-id=640033", "a=sendonly"], summary);
    }
}
