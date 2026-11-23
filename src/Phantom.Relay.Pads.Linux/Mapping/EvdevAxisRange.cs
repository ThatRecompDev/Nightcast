// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Pads.Linux.Mapping;

/// <summary>
/// One absolute axis as the device describes it (<c>struct input_absinfo</c>, <c>EVIOCGABS</c>): its value when it was
/// asked and its range. An axis the device lacks is the default, whose range is empty (<see cref="Present"/> false).
/// </summary>
internal readonly record struct EvdevAxisRange(int Value, int Minimum, int Maximum)
{
    private const float Half = 0.5f;

    /// <summary>True when the device has the axis: its range is not empty.</summary>
    public bool Present => Maximum > Minimum;

    /// <summary>A stick axis from -1 to 1, its centre the middle of the range; 0 for an axis the device lacks.</summary>
    [HotPath]
    public float Stick(int value)
    {
        if (!Present)
        {
            return 0f;
        }

        var centre = (Minimum + (float)Maximum) * Half;
        var travel = (Maximum - (float)Minimum) * Half;

        return Math.Clamp((value - centre) / travel, -1f, 1f);
    }

    /// <summary>A trigger from 0 (the range's minimum) to 1 (its maximum); 0 for an axis the device lacks.</summary>
    [HotPath]
    public float Trigger(int value)
    {
        if (!Present)
        {
            return 0f;
        }

        return Math.Clamp((value - (float)Minimum) / (Maximum - (float)Minimum), 0f, 1f);
    }
}
