// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg;

/// <summary>
/// A frame generator as the decoder's GPU path runs it, on the decoder's D3D11 device and thread: Lossless Scaling's
/// (<see cref="LsfgFrameStage"/>) or NVIDIA's FRUC (Phantom.Relay.Nvidia). Each new decoded picture (an NV12 surface)
/// goes in (<see cref="Process"/>), the frames between it and the one before come out as RGBA textures the stage owns,
/// and each is written into an NV12 render target (<see cref="Write"/>, a slot of the shared ring); the picture's work
/// then ends (<see cref="Done"/>). Only new pictures are given; the pacer shows the frames.
/// </summary>
public interface IFrameGenerationStage : IDisposable
{
    /// <summary>The last new picture as RGBA (the stage's own conversion of it), valid until the next picture.</summary>
    nint Picture { get; }

    /// <summary>The GPU time of the last picture whose time came back, in milliseconds; 0 before any.</summary>
    double LastGpuMilliseconds { get; }

    /// <summary>Why the stage stopped generating (it gives no frames from then on), or null while it works.</summary>
    string? Failure { get; }

    /// <summary>
    /// Takes a new decoded picture (slice <paramref name="slice"/> of the decoder's <paramref name="surface"/>) and puts
    /// the frames generated between the one before and it in <paramref name="generated"/>, in display order; how many.
    /// </summary>
    int Process(nint surface, uint slice, Span<nint> generated);

    /// <summary>Writes a generated frame into <paramref name="nv12"/>, an NV12 render target.</summary>
    bool Write(nint generated, nint nv12);

    /// <summary>The picture's work is all queued: its timing ends, and an earlier timing is read if it is back.</summary>
    void Done();
}
