// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// What a writer put in its file: the video's samples and key frames, the sound's packets, the lost packets it
/// concealed (a gap in the sound's timestamps), the packets it left out (late, repeated, before the video or not
/// Opus), the fragments, the bytes and the movie's length.
/// </summary>
public sealed record Mp4WriteCounts(
    long VideoSamples,
    long KeyFrames,
    long AudioPackets,
    long AudioConcealed,
    long AudioSkipped,
    int Fragments,
    long Bytes,
    TimeSpan Duration);
