// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe.Tests;

public sealed class AnswerSummaryTests
{
    [Fact]
    public void The_answer_summary_keeps_codecs_and_drops_credentials_and_candidates()
    {
        string answer = "v=0\r\nm=video 9 UDP/TLS/RTP/SAVPF 102\r\nb=AS:30000\r\na=ice-pwd:secret\r\na=candidate:1 1 udp 1 10.0.0.2 9 typ host\r\n"
            + "a=fingerprint:sha-256 AA\r\na=rtpmap:102 H264/90000\r\na=fmtp:102 profile-level-id=640033\r\na=sendonly\r\n";

        var summary = AnswerSummary.Of(answer);

        Assert.Equal(["m=video 9 UDP/TLS/RTP/SAVPF 102", "b=AS:30000", "a=rtpmap:102 H264/90000", "a=fmtp:102 profile-level-id=640033", "a=sendonly"], summary);
    }

    [Fact]
    public void The_answer_summary_keeps_header_extensions_and_rtcp_feedback()
    {
        string answer = "m=video 9 UDP/TLS/RTP/SAVPF 104\r\nb=TIAS:80000000\r\na=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01\r\n"
            + "a=rtcp-fb:104 goog-remb\r\na=rtcp-fb:104 transport-cc\r\na=ice-ufrag:abcd\r\n";

        var summary = AnswerSummary.Of(answer);

        Assert.Equal(["m=video 9 UDP/TLS/RTP/SAVPF 104", "b=TIAS:80000000", "a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01", "a=rtcp-fb:104 goog-remb", "a=rtcp-fb:104 transport-cc"], summary);
    }
}
