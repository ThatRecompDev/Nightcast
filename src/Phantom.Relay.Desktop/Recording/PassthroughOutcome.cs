// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Mp4;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// How a source.mp4 written by <see cref="PassthroughRecording"/> came out: what the writer put in it (null when no
/// file was made), why not or what failed (null when nothing did), the units before the first key frame, and the units
/// and sound packets dropped because a queue was full.
/// </summary>
internal sealed record PassthroughOutcome(Mp4WriteCounts? Counts, string? Problem, long SkippedUnits, long DroppedUnits, long DroppedPackets);
