// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop;

/// <summary>
/// How a run ended (<see cref="DesktopProgram"/>'s exit codes), whether its reason was already shown in the window and
/// closed there, and whether Nightcast starts again (<see cref="Restart"/>: lsfg-vk made ready, loaded at the next start).
/// </summary>
internal readonly record struct RunOutcome(int Code, bool ShownInWindow)
{
    public bool Restart { get; init; }
}
