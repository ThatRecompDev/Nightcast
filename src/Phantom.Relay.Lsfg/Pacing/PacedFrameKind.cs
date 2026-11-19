// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pacing;

/// <summary>What a refresh shows.</summary>
public enum PacedFrameKind
{
    /// <summary>Nothing new: keep showing what is on screen.</summary>
    Hold,

    /// <summary>A real frame.</summary>
    Real,

    /// <summary>A generated frame between two real frames.</summary>
    Generated,
}
