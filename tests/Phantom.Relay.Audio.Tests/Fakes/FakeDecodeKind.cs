// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Audio.Tests.Fakes;

/// <summary>What the player asked of the fake decoder.</summary>
public enum FakeDecodeKind
{
    /// <summary>A packet decoded.</summary>
    Packet,

    /// <summary>A lost packet rebuilt from the next one's FEC.</summary>
    Fec,

    /// <summary>A packet's worth concealed.</summary>
    Lost,
}
