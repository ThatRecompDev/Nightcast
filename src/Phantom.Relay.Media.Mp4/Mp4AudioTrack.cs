// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// An Opus track at 48 kHz (Opus in ISOBMFF always counts at 48 kHz, <see cref="Timescale"/>): its decoded channels
/// (1 or 2: channel mapping family 0) and its pre-skip, the samples a decoder drops from its start (its dOps).
/// </summary>
public sealed record Mp4AudioTrack(int Channels, int PreSkip)
{
    /// <summary>The track's time units a second: Opus's 48 kHz.</summary>
    public const int Timescale = 48000;
}
