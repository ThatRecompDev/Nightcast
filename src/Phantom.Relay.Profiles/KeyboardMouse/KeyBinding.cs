// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>A keyboard key (by its place on the keyboard) and what it drives while held.</summary>
public sealed record KeyBinding(Key Key, PadControl Control);
