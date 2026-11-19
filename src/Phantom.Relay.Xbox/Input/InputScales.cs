// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Input;

/// <summary>Scales normalised stick and trigger values to the report's integers, clamping out-of-range input.</summary>
public static class InputScales
{
    public static short Axis(float value)
    {
        return (short)MathF.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);
    }

    public static ushort Trigger(float value)
    {
        return (ushort)MathF.Round(Math.Clamp(value, 0f, 1f) * ushort.MaxValue);
    }
}
