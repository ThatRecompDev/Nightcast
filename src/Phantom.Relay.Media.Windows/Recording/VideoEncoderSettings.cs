// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Recording;

/// <summary>
/// What a recording is encoded at: the picture's size (made even, as NV12 needs), the nominal frame rate the encoder
/// budgets its bits for (the samples' own times set how the file plays), the mean bitrate and the codec.
/// </summary>
public sealed record VideoEncoderSettings(int Width, int Height, int FramesPerSecond, int BitsPerSecond, RecordedVideoCodec Codec)
{
    /// <summary>A bitrate high enough that the encoder hides no artifact of the pictures it records: 80 Mbit/s.</summary>
    public const int HighBitrate = 80_000_000;
}
