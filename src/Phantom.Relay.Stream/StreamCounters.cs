// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream;

/// <summary>
/// The stream's running totals at one moment, read on the network thread: frames received (access units assembled),
/// pictures decoded, units decoded and the time they took, the slowest decode since the last reading, packets lost,
/// keyframe requests, input reports sent and the picture's size.
/// </summary>
public readonly record struct StreamCounters(
    long FramesReceived,
    long FramesDecoded,
    long UnitsDecoded,
    TimeSpan DecodeTotal,
    TimeSpan DecodeWorst,
    long PacketsLost,
    long KeyframeRequests,
    long InputReportsSent,
    int Width,
    int Height);
