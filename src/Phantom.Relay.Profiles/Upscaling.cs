// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>The upscaler and its sharpening (0: none, 1: the most).</summary>
public sealed record Upscaling(UpscaleMethod Method, float Sharpness)
{
    public static Upscaling Standard { get; } = new(UpscaleMethod.Sharp, 0f);
}
