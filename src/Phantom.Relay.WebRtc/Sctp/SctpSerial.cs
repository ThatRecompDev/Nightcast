// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// Serial number arithmetic (RFC 1982) for TSNs and stream sequence numbers, which wrap: a number is after another
/// when it lies less than half the number space ahead of it.
/// </summary>
internal static class SctpSerial
{
    [HotPath]
    public static bool After(uint a, uint b)
    {
        return (int)(a - b) > 0;
    }

    [HotPath]
    public static bool After(ushort a, ushort b)
    {
        return (short)(a - b) > 0;
    }
}
