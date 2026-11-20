// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Nvidia;

/// <summary>The two surface formats FRUC takes: the decoder's own NV12, or 8-bit RGBA (FRUC's "ARGB").</summary>
public enum FrucSurfaceFormat
{
    Nv12,

    Rgba,
}
