// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// What the media path did: the network side's counts, then the decoder's pictures with their size, the decode time
/// per unit (average and worst), the deepest the decode queue got, and the decoder's problem if it could not open.
/// </summary>
public sealed record MediaReport(
    MediaCounts Counts,
    long Pictures,
    int Width,
    int Height,
    TimeSpan AverageDecode,
    TimeSpan WorstDecode,
    int DeepestQueue,
    string? DecoderProblem);
