// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// How fast checks go out and how long they wait. Tuned for latency: a home network answers in a few milliseconds, so
/// a pair is retried quickly and the whole check list gives up within a few seconds.
/// </summary>
public static class IceTiming
{
    /// <summary>The pacing between new checks (RFC 8445's Ta, at least 5 ms for a single stream).</summary>
    public static readonly TimeSpan Pacing = TimeSpan.FromMilliseconds(20);

    /// <summary>The first retransmission timeout of a check, doubled after each try.</summary>
    public static readonly TimeSpan FirstTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>How many times a check is sent before its pair fails.</summary>
    public const int MaxAttempts = 6;

    /// <summary>How often the selected pair is checked again once connected, which keeps NAT bindings and consent fresh.</summary>
    public static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(2);
}
