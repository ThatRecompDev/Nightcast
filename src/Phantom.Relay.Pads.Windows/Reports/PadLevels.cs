// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Reports;

/// <summary>The engine's feedback levels (0 to 1) as the bytes an output report carries (0 to 255).</summary>
public static class PadLevels
{
    private const float Full = byte.MaxValue;

    /// <summary>A level as a byte: clamped to 0..1, rounded to the nearest step; not a number reads as 0.</summary>
    public static byte ToByte(float level)
    {
        if (!(level > 0f))
        {
            return 0;
        }

        return (byte)MathF.Round(MathF.Min(level, 1f) * Full);
    }
}
