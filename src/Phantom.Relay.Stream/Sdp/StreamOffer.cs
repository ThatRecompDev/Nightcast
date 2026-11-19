// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Core.Randomness;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.Stream.Sdp;

/// <summary>
/// The client's SDP offer: the session's ICE credentials and DTLS certificate fingerprint on every m-line, the m-lines
/// a real client offers (Opus sendrecv, video recvonly, the data channels), bundled, and every video codec the engine
/// can decode, best first, so the console's answer picks the best it sends (docs/architecture/xhome.md). The
/// <see cref="BitrateFeedback"/> switches add a bandwidth line, goog-remb, and the transport-wide sequence extension
/// with transport-cc; off, the offer is the one before them.
/// </summary>
public static class StreamOffer
{
    /// <summary>The payload type the offer gives Opus; the console's sound arrives with it.</summary>
    public const int OpusPayload = 111;

    public const int H264HighPayload = 102;
    public const int H264MainPayload = 104;
    public const int H264BaselinePayload = 106;
    public const int Av1Payload = 45;

    /// <summary>The local id the offer gives the transport-wide sequence extension (the one browsers use).</summary>
    public const int TransportWideExtensionId = 3;

    private const int SctpPort = 5000;
    private const int MaxMessageSize = 262144;
    private const string Line = "\r\n";

    /// <summary>b=TIAS counts bit/s where b=AS counts kbit/s.</summary>
    private const long BitsPerKilobit = 1000;

    /// <summary>The H.264 levels offered: 5.1 (0x33) covers 1440p at 60 frames a second.</summary>
    private const string HighProfileLevel = "640033";
    private const string MainProfileLevel = "4d0033";
    private const string BaselineProfileLevel = "42e033";

    /// <summary>The offer without bitrate feedback.</summary>
    public static string Create(IceCredentials ice, string fingerprint, IRandomSource random)
    {
        return Create(ice, fingerprint, random, BitrateFeedback.Off);
    }

    /// <summary>
    /// An offer carrying <paramref name="ice"/> and the certificate <paramref name="fingerprint"/>, so the ICE agent
    /// and DTLS client that hold them can connect, with the <paramref name="feedback"/> switches that are on. The
    /// origin's session id is drawn from <paramref name="random"/>.
    /// </summary>
    public static string Create(IceCredentials ice, string fingerprint, IRandomSource random, BitrateFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        string transport = $"a=ice-ufrag:{ice.Ufrag}{Line}a=ice-pwd:{ice.Password}{Line}a=ice-options:trickle{Line}a=fingerprint:sha-256 {fingerprint}{Line}a=setup:actpass{Line}";

        var sdp = new StringBuilder();
        sdp.Append($"v=0{Line}o=- {random.NextUInt32()} 2 IN IP4 127.0.0.1{Line}s=-{Line}t=0 0{Line}");
        sdp.Append($"a=group:BUNDLE 0 1 2{Line}a=msid-semantic: WMS{Line}");
        AppendAudio(sdp, transport, feedback);
        AppendVideo(sdp, transport, feedback);
        AppendData(sdp, transport);

        return sdp.ToString();
    }

    private static void AppendAudio(StringBuilder sdp, string transport, BitrateFeedback feedback)
    {
        sdp.Append($"m=audio 9 UDP/TLS/RTP/SAVPF {OpusPayload}{Line}c=IN IP4 0.0.0.0{Line}a=rtcp:9 IN IP4 0.0.0.0{Line}");
        sdp.Append(transport);
        sdp.Append($"a=mid:0{Line}a=sendrecv{Line}a=rtcp-mux{Line}");
        AppendTransportWide(sdp, feedback);
        sdp.Append($"a=rtpmap:{OpusPayload} opus/48000/2{Line}a=fmtp:{OpusPayload} minptime=10;useinbandfec=1{Line}");

        if (feedback.TransportWideCc)
        {
            sdp.Append($"a=rtcp-fb:{OpusPayload} transport-cc{Line}");
        }
    }

    private static void AppendVideo(StringBuilder sdp, string transport, BitrateFeedback feedback)
    {
        sdp.Append($"m=video 9 UDP/TLS/RTP/SAVPF {H264HighPayload} {H264MainPayload} {H264BaselinePayload} {Av1Payload}{Line}");
        sdp.Append($"c=IN IP4 0.0.0.0{Line}");
        AppendBandwidth(sdp, feedback);

        sdp.Append($"a=rtcp:9 IN IP4 0.0.0.0{Line}");
        sdp.Append(transport);
        sdp.Append($"a=mid:1{Line}a=recvonly{Line}a=rtcp-mux{Line}a=rtcp-rsize{Line}");
        AppendTransportWide(sdp, feedback);

        // The codecs, best first: H.264 High, Main and Constrained Baseline, then AV1.
        AppendH264(sdp, H264HighPayload, HighProfileLevel, feedback);
        AppendH264(sdp, H264MainPayload, MainProfileLevel, feedback);
        AppendH264(sdp, H264BaselinePayload, BaselineProfileLevel, feedback);

        sdp.Append($"a=rtpmap:{Av1Payload} AV1/90000{Line}");
        AppendFeedback(sdp, Av1Payload, feedback);
    }

    private static void AppendH264(StringBuilder sdp, int payload, string profileLevel, BitrateFeedback feedback)
    {
        sdp.Append($"a=rtpmap:{payload} H264/90000{Line}");
        AppendFeedback(sdp, payload, feedback);
        sdp.Append($"a=fmtp:{payload} level-asymmetry-allowed=1;packetization-mode=1;profile-level-id={profileLevel}{Line}");
    }

    /// <summary>A video payload's RTCP feedback: NACK and PLI always, goog-remb and transport-cc when switched on.</summary>
    private static void AppendFeedback(StringBuilder sdp, int payload, BitrateFeedback feedback)
    {
        sdp.Append($"a=rtcp-fb:{payload} nack{Line}a=rtcp-fb:{payload} nack pli{Line}");

        if (feedback.RembKbps > 0)
        {
            sdp.Append($"a=rtcp-fb:{payload} goog-remb{Line}");
        }

        if (feedback.TransportWideCc)
        {
            sdp.Append($"a=rtcp-fb:{payload} transport-cc{Line}");
        }
    }

    /// <summary>The bandwidth the client takes in the video section: b=AS in kbit/s (RFC 4566), b=TIAS in bit/s (RFC 3890).</summary>
    private static void AppendBandwidth(StringBuilder sdp, BitrateFeedback feedback)
    {
        if (feedback.BandwidthKbps > 0)
        {
            sdp.Append($"b=AS:{feedback.BandwidthKbps}{Line}b=TIAS:{feedback.BandwidthKbps * BitsPerKilobit}{Line}");
        }
    }

    /// <summary>The transport-wide sequence number extension (RFC 8285 extmap), on sound and video alike as browsers offer it.</summary>
    private static void AppendTransportWide(StringBuilder sdp, BitrateFeedback feedback)
    {
        if (feedback.TransportWideCc)
        {
            sdp.Append($"a=extmap:{TransportWideExtensionId} {TransportFeedbackFormat.ExtensionUri}{Line}");
        }
    }

    private static void AppendData(StringBuilder sdp, string transport)
    {
        sdp.Append($"m=application 9 UDP/DTLS/SCTP webrtc-datachannel{Line}c=IN IP4 0.0.0.0{Line}");
        sdp.Append(transport);
        sdp.Append($"a=mid:2{Line}a=sctp-port:{SctpPort}{Line}a=max-message-size:{MaxMessageSize}{Line}");
    }
}
