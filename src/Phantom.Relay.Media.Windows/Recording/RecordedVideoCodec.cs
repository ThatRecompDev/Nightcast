// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Recording;

/// <summary>The codec a recording is encoded with.</summary>
public enum RecordedVideoCodec
{
    /// <summary>H.264 High: plays everywhere; the GPU's encoder takes up to 4096 x 4096.</summary>
    H264,

    /// <summary>HEVC Main: needs Windows' HEVC extension to play back in Windows itself.</summary>
    Hevc,
}
