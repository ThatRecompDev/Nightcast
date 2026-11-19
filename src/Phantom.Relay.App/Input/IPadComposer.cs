// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// Gathers one frame's input into the console's pad 0: the pads and keyboard and mouse played as the pad, shaped by the
/// profile. Called once a frame on the render thread with the frame's elapsed time; it allocates nothing.
/// </summary>
public interface IPadComposer
{
    XboxPadState Compose(InputFrame input, float elapsedSeconds);
}
