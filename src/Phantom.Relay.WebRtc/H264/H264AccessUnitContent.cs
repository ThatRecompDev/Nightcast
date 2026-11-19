// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.H264;

/// <summary>What an access unit holds besides ordinary slices.</summary>
[Flags]
public enum H264AccessUnitContent
{
    None = 0,

    /// <summary>An IDR slice: the access unit is a keyframe, decodable without what came before.</summary>
    Idr = 1,

    /// <summary>A sequence parameter set.</summary>
    Sps = 2,

    /// <summary>A picture parameter set.</summary>
    Pps = 4,
}
