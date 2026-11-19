// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The network side of the media path: packets decrypted and refused, RTCP and sound packets, video access units
/// assembled (and keyframes among them), units dropped as broken, packets lost, and keyframe requests sent.
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
    long KeyframeRequests);
