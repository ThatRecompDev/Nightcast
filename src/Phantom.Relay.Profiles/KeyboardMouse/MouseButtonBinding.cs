// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>A mouse button and what it drives while held.</summary>
public sealed record MouseButtonBinding(PointerButton Button, PadControl Control);
