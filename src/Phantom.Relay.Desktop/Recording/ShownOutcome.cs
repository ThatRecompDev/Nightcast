// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Recording;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// How shown.mp4 came out: what it holds (null when it was not made), why not, the encoder, the pictures written, how
/// much later than source.mp4 its first picture was shown, and the pictures not recorded: shown before the encoder was
/// ready, with no free texture in its pool (the encoder behind), dropped from a full queue, of another size than the
/// first (the upscaler changed), or refused by the encoder.
/// </summary>
internal sealed record ShownOutcome(
    Mp4Facts? Facts,
    string? Problem,
    string Encoder,
    long Written,
    TimeSpan Offset,
    long Warming,
    long Busy,
    long Dropped,
    long Resized,
    long Failed);
