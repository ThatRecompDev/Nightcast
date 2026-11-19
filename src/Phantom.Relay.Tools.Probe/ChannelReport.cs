// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// What the data channels did: the association's state, the channels that opened, how many messages each way, the
/// message kinds the console sent (types and targets only, never contents), and how long until the first RTP packet.
/// </summary>
public sealed record ChannelReport(
    string Association,
    IReadOnlyList<string> Opened,
    int MessagesReceived,
    int MessagesSent,
    IReadOnlyList<string> ConsoleKinds,
    TimeSpan? FirstRtpAfter);
