// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Turn;

/// <summary>How a TURN client paces its requests and keeps its allocation, permissions and channels alive.</summary>
public static class TurnTiming
{
    /// <summary>The first retransmission timeout of a request, doubled after each try (RFC 8489's RTO).</summary>
    public static readonly TimeSpan FirstTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>How many times a request is sent before it fails.</summary>
    public const int MaxAttempts = 5;

    /// <summary>The allocation's lifetime when the server names none (RFC 8656's default).</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A channel binding lasts 10 minutes and the permission it installs 5: binding again every 4 minutes keeps both.
    /// </summary>
    public static readonly TimeSpan ChannelRefresh = TimeSpan.FromMinutes(4);
}
