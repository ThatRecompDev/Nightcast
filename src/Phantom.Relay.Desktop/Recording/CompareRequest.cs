// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// One side-by-side comparison for ffmpeg to make: the source and shown files, the output, how much later than the
/// source the shown file starts (the same moment on both sides), the source's size (the shown side is scaled to it),
/// the rate both are brought to, the labels, a font file for them (null: ffmpeg's own), and whether at half speed.
/// </summary>
internal sealed record CompareRequest(
    string Source,
    string Shown,
    string Output,
    TimeSpan ShownOffset,
    int Width,
    int Height,
    int FramesPerSecond,
    string SourceLabel,
    string ShownLabel,
    string? FontFile,
    bool HalfSpeed);
