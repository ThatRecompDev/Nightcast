// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// What the media path did: the network side's counts, then the decoder's pictures with their size, the decode time
/// per unit (average and worst), the deepest the decode queue got, and the decoder's problem if it threw; and the
/// video's average and peak bitrate and the sound's average over the seconds that carried video.
/// </summary>
public sealed record MediaReport(
    MediaCounts Counts,
    long Pictures,
    int Width,
    int Height,
    TimeSpan AverageDecode,
    TimeSpan WorstDecode,
    int DeepestQueue,
    string? DecoderProblem)
{
    public double VideoAverageMegabits { get; init; }

    public double VideoPeakMegabits { get; init; }

    public double AudioAverageMegabits { get; init; }
}
