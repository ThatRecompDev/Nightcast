// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop;

/// <summary>How a run ended (<see cref="DesktopProgram"/>'s exit codes), and whether its reason was already shown in the window and closed there.</summary>
internal readonly record struct RunOutcome(int Code, bool ShownInWindow);
