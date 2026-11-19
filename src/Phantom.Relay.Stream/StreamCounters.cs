// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream;

/// <summary>
/// The stream's running totals at one moment, read on the network thread: frames received (access units assembled),
/// pictures decoded, units decoded and the time they took (and of it the hand-off of their pictures to the renderer),
/// the slowest decode since the last reading, packets lost, keyframe requests, input reports sent, the picture's
/// size, and the units that carried new pictures and repeats with the cadence they follow.
/// </summary>
public readonly record struct StreamCounters(
    long FramesReceived,
    long FramesDecoded,
    long UnitsDecoded,
    long NewPictures,
    long RepeatedPictures,
    PictureCadence Cadence,
    TimeSpan DecodeTotal,
    TimeSpan HandOffTotal,
    TimeSpan DecodeWorst,
    long PacketsLost,
    long KeyframeRequests,
    long InputReportsSent,
    int Width,
    int Height);
