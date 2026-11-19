// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Audio;

/// <summary>How the jitter buffer trades latency for safety.</summary>
/// <param name="MinDelayMilliseconds">The least playout delay it shrinks to when the arrivals are steady.</param>
/// <param name="MaxDelayMilliseconds">The most it grows to on jitter and underruns.</param>
/// <param name="StartPackets">The delay it starts at, in packets of the first packet's duration.</param>
public sealed record PlayoutSettings(int MinDelayMilliseconds, int MaxDelayMilliseconds, int StartPackets)
{
    public const int DefaultMinDelayMilliseconds = 10;

    public const int DefaultMaxDelayMilliseconds = 60;

    public const int DefaultStartPackets = 2;

    /// <summary>Latency first: 2 packets to start (20 ms of the console's 10 ms frames), between 10 and 60 ms.</summary>
    public static PlayoutSettings Default { get; } =
        new(DefaultMinDelayMilliseconds, DefaultMaxDelayMilliseconds, DefaultStartPackets);
}
