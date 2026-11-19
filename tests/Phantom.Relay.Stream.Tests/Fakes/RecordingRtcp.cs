// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Link;

namespace Phantom.Relay.Stream.Tests.Fakes;

/// <summary>Keeps a copy of every protected RTCP packet the media path sends.</summary>
public sealed class RecordingRtcp : IRtcpOutput
{
    public List<byte[]> Sent { get; } = [];

    public void Send(ReadOnlySpan<byte> packet)
    {
        Sent.Add(packet.ToArray());
    }
}
