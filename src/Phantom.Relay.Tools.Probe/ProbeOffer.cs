// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Core.Randomness;
using Phantom.Relay.WebRtc.Ice;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// A well-formed SDP offer with no WebRTC stack behind it: random ICE credentials and a random DTLS fingerprint, the
/// m-lines a real client offers (Opus sendrecv, video recvonly, the data channels) and every video codec the engine
/// can decode, best first, so the console's answer shows which codec and profile it picks. Nothing ever connects.
/// </summary>
public static class ProbeOffer
{
    public const string OpusPayload = "111";
    public const string H264HighPayload = "102";
    public const string H264MainPayload = "104";
    public const string H264BaselinePayload = "106";
    public const string Av1Payload = "45";

    private const int UfragBytes = 4;
    private const int PasswordBytes = 12;
    private const int FingerprintBytes = 32;
    private const int HexDigitsPerByte = 2;
    private const int SctpPort = 5000;
    private const int MaxMessageSize = 262144;
    private const string Line = "\r\n";

    /// <summary>The H.264 levels offered: 5.1 (0x33) covers 1440p at 60 frames a second.</summary>
    private const string HighProfileLevel = "640033";
    private const string MainProfileLevel = "4d0033";
    private const string BaselineProfileLevel = "42e033";

    /// <summary>An offer with random ICE credentials and fingerprint: for a probe that only reads the answer.</summary>
    public static string Create(IRandomSource random)
    {
        return Create(new IceCredentials(Hex(random, UfragBytes), Hex(random, PasswordBytes)), Fingerprint(random), random);
    }

    /// <summary>
    /// An offer carrying <paramref name="ice"/> and the certificate <paramref name="fingerprint"/>, so a live ICE agent
    /// and DTLS client with them can connect.
    /// </summary>
    public static string Create(IceCredentials ice, string fingerprint, IRandomSource random)
    {
        string transport = $"a=ice-ufrag:{ice.Ufrag}{Line}a=ice-pwd:{ice.Password}{Line}a=ice-options:trickle{Line}a=fingerprint:sha-256 {fingerprint}{Line}a=setup:actpass{Line}";

        var sdp = new StringBuilder();
        sdp.Append($"v=0{Line}o=- {random.NextUInt32()} 2 IN IP4 127.0.0.1{Line}s=-{Line}t=0 0{Line}");
        sdp.Append($"a=group:BUNDLE 0 1 2{Line}a=msid-semantic: WMS{Line}");
        AppendAudio(sdp, transport);
        AppendVideo(sdp, transport);
        AppendData(sdp, transport);

        return sdp.ToString();
    }

    private static void AppendAudio(StringBuilder sdp, string transport)
    {
        sdp.Append($"m=audio 9 UDP/TLS/RTP/SAVPF {OpusPayload}{Line}c=IN IP4 0.0.0.0{Line}a=rtcp:9 IN IP4 0.0.0.0{Line}");
        sdp.Append(transport);
        sdp.Append($"a=mid:0{Line}a=sendrecv{Line}a=rtcp-mux{Line}");
        sdp.Append($"a=rtpmap:{OpusPayload} opus/48000/2{Line}a=fmtp:{OpusPayload} minptime=10;useinbandfec=1{Line}");
    }

    private static void AppendVideo(StringBuilder sdp, string transport)
    {
        sdp.Append($"m=video 9 UDP/TLS/RTP/SAVPF {H264HighPayload} {H264MainPayload} {H264BaselinePayload} {Av1Payload}{Line}");
        sdp.Append($"c=IN IP4 0.0.0.0{Line}a=rtcp:9 IN IP4 0.0.0.0{Line}");
        sdp.Append(transport);
        sdp.Append($"a=mid:1{Line}a=recvonly{Line}a=rtcp-mux{Line}a=rtcp-rsize{Line}");

        // The codecs, best first: H.264 High, Main and Constrained Baseline, then AV1.
        AppendH264(sdp, H264HighPayload, HighProfileLevel);
        AppendH264(sdp, H264MainPayload, MainProfileLevel);
        AppendH264(sdp, H264BaselinePayload, BaselineProfileLevel);

        sdp.Append($"a=rtpmap:{Av1Payload} AV1/90000{Line}");
        sdp.Append($"a=rtcp-fb:{Av1Payload} nack{Line}a=rtcp-fb:{Av1Payload} nack pli{Line}");
    }

    private static void AppendH264(StringBuilder sdp, string payload, string profileLevel)
    {
        sdp.Append($"a=rtpmap:{payload} H264/90000{Line}");
        sdp.Append($"a=rtcp-fb:{payload} nack{Line}a=rtcp-fb:{payload} nack pli{Line}");
        sdp.Append($"a=fmtp:{payload} level-asymmetry-allowed=1;packetization-mode=1;profile-level-id={profileLevel}{Line}");
    }

    private static void AppendData(StringBuilder sdp, string transport)
    {
        sdp.Append($"m=application 9 UDP/DTLS/SCTP webrtc-datachannel{Line}c=IN IP4 0.0.0.0{Line}");
        sdp.Append(transport);
        sdp.Append($"a=mid:2{Line}a=sctp-port:{SctpPort}{Line}a=max-message-size:{MaxMessageSize}{Line}");
    }

    private static string Hex(IRandomSource random, int bytes)
    {
        Span<byte> buffer = stackalloc byte[bytes];
        Fill(random, buffer);

        return Convert.ToHexStringLower(buffer);
    }

    private static string Fingerprint(IRandomSource random)
    {
        Span<byte> buffer = stackalloc byte[FingerprintBytes];
        Fill(random, buffer);

        return string.Join(':', Convert.ToHexString(buffer).Chunk(HexDigitsPerByte).Select(static pair => new string(pair)));
    }

    private static void Fill(IRandomSource random, Span<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)random.NextUInt32();
        }
    }
}
