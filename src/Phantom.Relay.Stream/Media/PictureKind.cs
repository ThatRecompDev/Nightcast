// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>What an access unit carries (<see cref="RepeatClassifier"/>).</summary>
public enum PictureKind
{
    /// <summary>A picture the game drew.</summary>
    New,

    /// <summary>The picture before it again: the console sends 60 a second whatever the game draws.</summary>
    Repeat,
}
