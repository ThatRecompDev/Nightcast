// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Sdp;
using Phantom.Relay.WebRtc.Ice;

namespace Phantom.Relay.Stream.Tests.Sdp;

public sealed class StreamOfferTests
{
    private const string Fingerprint = "AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89";

    private static readonly IceCredentials Ice = new("u1f2", "0123456789abcdef0123456789abcdef");

    [Fact]
    public void The_offer_bundles_sound_video_and_data_with_crlf_lines()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7));

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
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7));

        Assert.Contains("a=fmtp:102 level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=640033", offer, StringComparison.Ordinal);
        Assert.Contains("a=fmtp:104 level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=4d0033", offer, StringComparison.Ordinal);
        Assert.Contains("a=rtpmap:45 AV1/90000", offer, StringComparison.Ordinal);
        Assert.Contains("a=mid:1\r\na=recvonly", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void Sound_is_opus_both_ways_with_in_band_error_correction()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7));

        Assert.Contains("a=rtpmap:111 opus/48000/2\r\na=fmtp:111 minptime=10;useinbandfec=1", offer, StringComparison.Ordinal);
        Assert.Contains("a=mid:0\r\na=sendrecv", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_m_line_carries_the_sessions_ice_credentials_and_certificate_fingerprint()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7));
        var lines = offer.Split("\r\n");

        Assert.Equal(3, lines.Count(static line => line == "a=ice-ufrag:u1f2"));
        Assert.Equal(3, lines.Count(static line => line == "a=ice-pwd:0123456789abcdef0123456789abcdef"));
        Assert.Equal(3, lines.Count(static line => line == $"a=fingerprint:sha-256 {Fingerprint}"));
        Assert.Equal(3, lines.Count(static line => line == "a=setup:actpass"));
    }

    [Fact]
    public void The_data_channels_use_sctp_port_5000()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7));

        Assert.Contains("a=mid:2\r\na=sctp-port:5000\r\na=max-message-size:262144", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void The_origin_session_id_comes_from_the_random_source()
    {
        var random = new SeededRandomSource(7);

        string first = StreamOffer.Create(Ice, Fingerprint, random);
        string second = StreamOffer.Create(Ice, Fingerprint, random);

        Assert.NotEqual(first.Split("\r\n")[1], second.Split("\r\n")[1]);
        Assert.Equal(StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7)), first);
    }

    [Fact]
    public void Without_feedback_the_offer_is_the_one_before_it()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7));

        Assert.Equal(offer, StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7), BitrateFeedback.Off));
        Assert.DoesNotContain("b=", offer, StringComparison.Ordinal);
        Assert.DoesNotContain("a=extmap", offer, StringComparison.Ordinal);
        Assert.DoesNotContain("goog-remb", offer, StringComparison.Ordinal);
        Assert.DoesNotContain("transport-cc", offer, StringComparison.Ordinal);
        Assert.Contains("m=video 9 UDP/TLS/RTP/SAVPF 102 104 106 45\r\nc=IN IP4 0.0.0.0\r\na=rtcp:9 IN IP4 0.0.0.0\r\n", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void The_bandwidth_line_follows_the_video_connection_line_in_kbit_and_bit_per_second()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7), new BitrateFeedback { BandwidthKbps = 80000 });

        Assert.Contains("m=video 9 UDP/TLS/RTP/SAVPF 102 104 106 45\r\nc=IN IP4 0.0.0.0\r\nb=AS:80000\r\nb=TIAS:80000000\r\na=rtcp:9", offer, StringComparison.Ordinal);
        Assert.Single(offer.Split("\r\n"), static line => line.StartsWith("b=AS:", StringComparison.Ordinal));
        Assert.DoesNotContain("goog-remb", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void Remb_offers_goog_remb_on_every_video_payload_alone()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7), new BitrateFeedback { RembKbps = 80000 });
        var lines = offer.Split("\r\n");

        Assert.Equal(["a=rtcp-fb:102 goog-remb", "a=rtcp-fb:104 goog-remb", "a=rtcp-fb:106 goog-remb", "a=rtcp-fb:45 goog-remb"], lines.Where(static line => line.EndsWith("goog-remb", StringComparison.Ordinal)));
        Assert.DoesNotContain("b=", offer, StringComparison.Ordinal);
        Assert.DoesNotContain("transport-cc", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void Transport_wide_cc_offers_the_extension_on_sound_and_video_and_transport_cc_on_every_payload()
    {
        string offer = StreamOffer.Create(Ice, Fingerprint, new SeededRandomSource(7), new BitrateFeedback { TransportWideCc = true });
        var lines = offer.Split("\r\n");

        Assert.Equal(2, lines.Count(static line => line == "a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01"));
        Assert.Equal(["a=rtcp-fb:111 transport-cc", "a=rtcp-fb:102 transport-cc", "a=rtcp-fb:104 transport-cc", "a=rtcp-fb:106 transport-cc", "a=rtcp-fb:45 transport-cc"], lines.Where(static line => line.EndsWith("transport-cc", StringComparison.Ordinal)));
        Assert.DoesNotContain("goog-remb", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void The_answer_says_which_feedback_it_kept()
    {
        string answer = "v=0\r\nm=video 9 UDP/TLS/RTP/SAVPF 104\r\nb=AS:50000\r\na=extmap:1 urn:ietf:params:rtp-hdrext:toffset\r\n"
            + "a=extmap:5/sendonly http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01\r\n"
            + "a=rtcp-fb:104 nack pli\r\na=rtcp-fb:104 transport-cc\r\n";

        var kept = AnswerFeedback.Of(answer);

        Assert.Equal(new AnswerFeedback(5, false, true, "b=AS:50000"), kept);
        Assert.Equal("transport-wide extmap id 5, transport-cc, b=AS:50000", kept.Describe());
        Assert.Equal(new AnswerFeedback(0, false, false, null), AnswerFeedback.Of("v=0\r\na=rtcp-fb:104 nack\r\n"));
        Assert.Equal("nothing", AnswerFeedback.Of(string.Empty).Describe());
        Assert.True(AnswerFeedback.Of("a=rtcp-fb:104 goog-remb\n").Remb);
    }

    [Fact]
    public void The_answers_h264_payload_types_are_read_back()
    {
        string answer = "v=0\r\nm=video 9 UDP/TLS/RTP/SAVPF 104 106\r\na=rtpmap:104 H264/90000\r\na=rtpmap:106 H264/90000\r\na=rtpmap:111 opus/48000/2\r\n";

        var payloads = VideoPayloads.Of(answer);

        Assert.Equal([104, 106], payloads.Order());
    }
}
