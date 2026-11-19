// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// What the media path did with the stream: packets decrypted and refused, RTCP and sound packets, video access
/// units assembled (and keyframes among them), units dropped as broken, packets lost, pictures decoded with their
/// size, the decode time per unit (average and worst), and the decoder's problem if it could not open.
/// </summary>
public sealed record MediaReport(
    long Decrypted,
    long Refused,
    long Rtcp,
    long Audio,
    long AccessUnits,
    long Keyframes,
    long Dropped,
    long Lost,
    long Pictures,
    int Width,
    int Height,
    TimeSpan AverageDecode,
    TimeSpan WorstDecode,
    string? DecoderProblem);
