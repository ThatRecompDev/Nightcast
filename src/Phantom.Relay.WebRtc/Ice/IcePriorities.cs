// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>Candidate and pair priorities (RFC 8445 sections 5.1.2 and 6.1.2.3).</summary>
public static class IcePriorities
{
    public const int HostPreference = 126;

    public const int PeerReflexivePreference = 110;

    public const int ServerReflexivePreference = 100;

    public const int RelayedPreference = 0;

    /// <summary>The local preference of a single-homed agent.</summary>
    public const int SingleLocalPreference = 65535;

    public const int RtpComponent = 1;

    private const int TypeShift = 24;
    private const int LocalShift = 8;
    private const int ComponentBase = 256;
    private const int PairHighShift = 32;

    /// <summary>(2^24)·type preference + (2^8)·local preference + (256 − component).</summary>
    public static uint Candidate(int typePreference, int localPreference, int component)
    {
        return ((uint)typePreference << TypeShift) | ((uint)localPreference << LocalShift) | (uint)(ComponentBase - component);
    }

    public static int TypePreference(IceCandidateKind kind)
    {
        switch (kind)
        {
            case IceCandidateKind.Host:
                return HostPreference;
            case IceCandidateKind.PeerReflexive:
                return PeerReflexivePreference;
            case IceCandidateKind.ServerReflexive:
                return ServerReflexivePreference;
            default:
                return RelayedPreference;
        }
    }

    /// <summary>2^32·min(G, D) + 2·max(G, D) + (G &gt; D ? 1 : 0), G the controlling side's priority.</summary>
    public static ulong Pair(uint controlling, uint controlled)
    {
        ulong low = Math.Min(controlling, controlled);
        ulong high = Math.Max(controlling, controlled);
        ulong tie = 0;

        if (controlling > controlled)
        {
            tie = 1;
        }

        return (low << PairHighShift) + (high << 1) + tie;
    }
}
