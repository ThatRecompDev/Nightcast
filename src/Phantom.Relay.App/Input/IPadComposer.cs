// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Profiles;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// Gathers one frame's input into the console's pad 0: the pads and keyboard and mouse played as the pad, shaped by the
/// profile. Called once a frame on the render thread with the frame's elapsed time; it allocates nothing, except when
/// the running game's profile changes (<see cref="Use"/>).
/// </summary>
public interface IPadComposer
{
    XboxPadState Compose(InputFrame input, float elapsedSeconds);

    /// <summary>Plays under <paramref name="profile"/> from the next frame on.</summary>
    void Use(StreamProfile profile);
}
