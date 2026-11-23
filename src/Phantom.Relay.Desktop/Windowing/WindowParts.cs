// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>
/// What the platform gives the window's application: the device and the window, the system's fonts, the pads' motors
/// (null where the pads have none) and the window's fullscreen.
/// </summary>
internal sealed record WindowParts(IGraphicsDevice Device, IWindow Window, ISystemFonts Fonts, IGamepadFeedback? Motors, IWindowMode Mode);
