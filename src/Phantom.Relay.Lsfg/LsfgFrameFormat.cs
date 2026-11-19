// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg;

/// <summary>The real frames' pixel format: eight bits per channel, red first or blue first.</summary>
public enum LsfgFrameFormat
{
    /// <summary>DXGI_FORMAT_R8G8B8A8_UNORM, what the NV12 converter writes.</summary>
    Rgba8,

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM, a swap chain's usual format.</summary>
    Bgra8,
}
