// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>How a stick's travel past the dead zone maps to the value sent.</summary>
public enum ResponseCurve
{
    Linear,

    /// <summary>Smoothstep: gentle near the centre and the edge.</summary>
    Smooth,

    /// <summary>Squared: fine control near the centre.</summary>
    Precise,

    /// <summary>Square root: fast response near the centre.</summary>
    Aggressive,
}
