// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// Gathers one frame's input into the console's pad 0: the pads, and later keyboard and mouse played as the pad and the
/// profile's shaping. Called once a frame on the render thread; it allocates nothing.
/// </summary>
public interface IPadComposer
{
    XboxPadState Compose(InputFrame input);
}
