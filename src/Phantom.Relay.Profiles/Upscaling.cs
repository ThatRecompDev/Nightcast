// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles;

/// <summary>
/// The upscaler and its sharpening (0: none, 1: the most). FSR 1 sharpens with its own default (RCAS at 0.2 stops, the
/// engine's <c>PictureUpscaler.FsrSharpness</c>); <see cref="Sharpness"/> is kept for a later setting of it.
/// </summary>
public sealed record Upscaling(UpscaleMethod Method, float Sharpness)
{
    public static Upscaling Standard { get; } = new(UpscaleMethod.Fsr, 0f);
}
