// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>A wheel direction and what each of its notches presses, for <see cref="WheelSettings.PressSeconds"/>.</summary>
public sealed record WheelBinding(WheelDirection Direction, PadControl Control);
