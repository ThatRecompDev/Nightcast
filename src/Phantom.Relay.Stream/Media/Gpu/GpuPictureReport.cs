// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// How the pictures reach the screen, for the stats: the path (left on the GPU, or read back to the CPU and why), what
/// frame generation does (its mode and GPU time, or why it is off) and what the decoder's own upscaler does
/// (<see cref="Upscale"/>: its method, size and GPU time, or why it is off; empty when none is asked for). Replaced
/// whole when any changes.
/// </summary>
public sealed record GpuPictureReport(string Path, string FrameGeneration, string Upscale = "")
{
    public const string ReadBack = "CPU read-back";

    public const string ZeroCopy = "GPU zero-copy";

    public const string GenerationOff = "LSFG off";

    /// <summary>Before the renderer offered its device: read back, no frame generation.</summary>
    public static GpuPictureReport Idle { get; } = new(ReadBack, GenerationOff);
}
