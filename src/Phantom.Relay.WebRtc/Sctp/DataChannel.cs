// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// One data channel: an SCTP stream both ways, named by its options. Made by <see cref="DataChannelHub"/> when this
/// side opens it or the peer does; messages go through the hub.
/// </summary>
public sealed class DataChannel
{
    public DataChannel(ushort id, DataChannelOptions options, DataChannelState state)
    {
        Id = id;
        Options = options;
        State = state;
        Policy = options.Policy;
        OpeningPolicy = options.Policy with { Unordered = false };
    }

    /// <summary>The SCTP stream identifier, both ways.</summary>
    public ushort Id { get; }

    public DataChannelOptions Options { get; }

    public string Label => Options.Label;

    public string Protocol => Options.Protocol;

    public DataChannelState State { get; internal set; }

    /// <summary>How each message travels once the channel is open.</summary>
    internal SctpSendPolicy Policy { get; }

    /// <summary>How each message travels before the ACK: ordered, so it cannot overtake the OPEN (RFC 8832 section 6).</summary>
    internal SctpSendPolicy OpeningPolicy { get; }
}
