// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// The network side of the media path: packets decrypted and refused, RTCP and sound packets, video access units
/// assembled (and keyframes among them), units dropped as broken, packets lost, and keyframe requests sent; the RTP
/// payload octets of video and sound, and the bitrate feedback sent (REMB and transport feedback packets).
/// </summary>
public sealed record MediaCounts(
    long Decrypted,
    long Refused,
    long Rtcp,
    long Audio,
    long AccessUnits,
    long Keyframes,
    long Dropped,
    long Lost,
    long KeyframeRequests)
{
    public long VideoBytes { get; init; }

    public long AudioBytes { get; init; }

    public long RembSent { get; init; }

    public long TransportFeedbackSent { get; init; }
}
