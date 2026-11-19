// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>The rhythm of new pictures in the console's 60 a second, as <see cref="RepeatClassifier"/> follows it.</summary>
public enum PictureCadence
{
    /// <summary>Every unit is taken as new: a 60 fps game, or no steady rhythm seen.</summary>
    Full,

    /// <summary>Every other unit repeats the one before: a 30 fps game.</summary>
    Half,
}
