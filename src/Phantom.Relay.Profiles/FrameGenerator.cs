// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>
/// Which frame generator makes the frames between the stream's own: Lossless Scaling's (docs/architecture/lsfg.md) or
/// NVIDIA's FRUC on the GPU's optical flow accelerator (docs/architecture/nvidia-fruc.md).
/// </summary>
public enum FrameGenerator
{
    Lsfg,

    NvidiaFruc,
}
