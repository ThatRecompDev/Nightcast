// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>
/// A PlayStation pad's analog bytes as the engine reads them. A stick axis is 0 to 255 with 128 at rest, x to the right
/// and y down (the HID convention, which the engine shares, so y is not turned); a trigger is 0 (released) to 255.
/// </summary>
public static class PadAxes
{
    private const float Centre = 128f;
    private const float HalfTravel = 127f;
    private const float TriggerTravel = byte.MaxValue;

    /// <summary>A stick axis from -1 to 1 (0 reads a little past -1 and is clamped).</summary>
    [HotPath]
    public static float Stick(byte value)
    {
        return Math.Clamp((value - Centre) / HalfTravel, -1f, 1f);
    }

    /// <summary>A trigger from 0 to 1.</summary>
    [HotPath]
    public static float Trigger(byte value)
    {
        return value / TriggerTravel;
    }
}
