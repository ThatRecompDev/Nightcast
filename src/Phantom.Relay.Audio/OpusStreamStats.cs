// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Audio;

/// <summary>A snapshot of the player, read from any thread.</summary>
/// <param name="DelayMilliseconds">
/// The playout delay, smoothed: how long a packet waits from its arrival until its first sample is rendered. The
/// device's own latency comes on top (<c>IAudioDevice.Format.LatencySeconds</c>).
/// </param>
/// <param name="TargetMilliseconds">The delay the buffer aims at now.</param>
/// <param name="Underruns">Times the buffer ran dry and had to conceal while waiting.</param>
/// <param name="ConcealedFrames">Packets' worth of sound concealed: lost, malformed or waited for.</param>
/// <param name="FecRecoveries">Lost packets rebuilt from the next packet's in-band FEC.</param>
/// <param name="LateDrops">Packets that arrived after their time had been played.</param>
/// <param name="DriftRatio">The resampling ratio holding the delay: above 1 plays faster than the device's clock.</param>
/// <param name="PacketsPlayed">Packets decoded and played.</param>
/// <param name="SkippedPackets">Packets dropped to cut a backlog (a burst after a stall) back to the target.</param>
/// <param name="Resyncs">Times the sequence numbers jumped and the buffer started over.</param>
/// <param name="RefusedPackets">Packets refused on arrival: malformed, too large, or with no free slot.</param>
/// <param name="Playing">False while the buffer fills, at the start or after a long outage.</param>
public readonly record struct OpusStreamStats(
    double DelayMilliseconds,
    double TargetMilliseconds,
    long Underruns,
    long ConcealedFrames,
    long FecRecoveries,
    long LateDrops,
    double DriftRatio,
    long PacketsPlayed,
    long SkippedPackets,
    long Resyncs,
    long RefusedPackets,
    bool Playing);
