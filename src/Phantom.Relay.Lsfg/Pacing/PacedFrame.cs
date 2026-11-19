// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pacing;

/// <summary>
/// The frame a refresh shows. For a real frame, <see cref="Frame"/> is its index. For a generated one, it lies between
/// real frames <see cref="Frame"/> - 1 and <see cref="Frame"/>, at step <see cref="Step"/> of <see cref="Steps"/>
/// (step 1 of 2 is the middle; <see cref="Steps"/> is the multiplier).
/// </summary>
public readonly record struct PacedFrame(PacedFrameKind Kind, long Frame, int Step, int Steps)
{
    /// <summary>Keep the current frame.</summary>
    public static PacedFrame Hold => default;
}
