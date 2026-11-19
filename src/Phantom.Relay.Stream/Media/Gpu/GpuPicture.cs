// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// A picture handed from the decode thread to the renderer on the GPU, as a few numbers: the slot of the ring
/// (<see cref="GpuPictureRing"/>) that holds it, the value the ring's fence reaches once it is written there, its
/// visible size (the slot may be larger: a decoder's padded surface), its colour, and when the decode thread had it
/// ready (a timestamp of the stream's clock), for the time from decode to present. <see cref="Generation"/> names the
/// ring it is in, which changes when the decoder makes its ring again. A generated picture was made between two decoded
/// ones (frame generation).
/// </summary>
public readonly record struct GpuPicture(
    int Generation,
    int Slot,
    ulong ReadyValue,
    int Width,
    int Height,
    VideoColour Colour,
    long DecodedAt,
    bool Generated);
