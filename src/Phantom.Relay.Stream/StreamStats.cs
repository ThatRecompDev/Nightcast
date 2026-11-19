// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream;

/// <summary>
/// One immutable snapshot of a stream, published by its network thread on every state change and once a second, so
/// the render thread reads it whole without a lock: the state (and why it failed), the running title, the path's
/// round trip, how long DTLS took, the frames received and decoded per second, the decode time per frame (average and
/// worst over the last second), and the packets lost, keyframe requests and input reports sent so far.
/// </summary>
public sealed record StreamStats
{
    /// <summary>A connection that has not started.</summary>
    public static StreamStats Idle { get; } = new();

    public StreamState State { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>The running game's title id, once the console named it.</summary>
    public string? TitleId { get; init; }

    /// <summary>The selected path's latest round trip, renewed by ICE's keep-alives.</summary>
    public TimeSpan? IceRoundTrip { get; init; }

    /// <summary>How long the DTLS handshake took.</summary>
    public TimeSpan? DtlsTime { get; init; }

    public double FramesReceivedPerSecond { get; init; }

    public double FramesDecodedPerSecond { get; init; }

    public TimeSpan DecodeAverage { get; init; }

    public TimeSpan DecodeWorst { get; init; }

    /// <summary>
    /// The CPU's part of handing each decoded picture to the renderer, averaged over the last second: the read-back to
    /// the CPU, or with the pictures left on the GPU the queueing of their copy. Part of <see cref="DecodeAverage"/>.
    /// </summary>
    public TimeSpan HandOffAverage { get; init; }

    public long PacketsLost { get; init; }

    public long KeyframeRequests { get; init; }

    public long InputReportsSent { get; init; }

    /// <summary>The decoded picture's size, once one was decoded.</summary>
    public int Width { get; init; }

    public int Height { get; init; }
}
