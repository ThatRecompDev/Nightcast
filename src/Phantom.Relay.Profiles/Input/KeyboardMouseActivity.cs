// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// Whether the player acted on keyboard or mouse in an input frame: a key or mouse button went down, the wheel turned,
/// or the mouse moved at least <see cref="MouseCounts"/>. A key held from before, or a mouse's single-count jitter, is
/// no act: only a fresh one may take the console's pad from a controller.
/// </summary>
public static class KeyboardMouseActivity
{
    /// <summary>How far the mouse moves in a frame, in counts, before it counts as moved: past a resting sensor's jitter.</summary>
    public const float MouseCounts = 2f;

    [HotPath]
    public static bool Acted(InputFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.PointerDelta.LengthSquared() >= MouseCounts * MouseCounts || frame.Wheel != default)
        {
            return true;
        }

        foreach (ref readonly var inputEvent in frame.Events)
        {
            if (inputEvent.Kind == InputEventKind.PointerDown || (inputEvent.Kind == InputEventKind.KeyDown && !inputEvent.Repeat))
            {
                return true;
            }
        }

        return false;
    }
}
