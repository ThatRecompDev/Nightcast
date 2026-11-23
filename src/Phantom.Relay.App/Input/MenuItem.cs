// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Input;

/// <summary>The rows of Relay's pad menu (<see cref="PadMenu"/>), in their order; each does what its key does.</summary>
public enum MenuItem
{
    /// <summary>The stats block (F3).</summary>
    Stats,

    /// <summary>Off, LSFG, NVIDIA FRUC (F4).</summary>
    FrameGeneration,

    /// <summary>LSFG's next setting (F5).</summary>
    LsfgSetting,

    /// <summary>x2, x3, x4 (F8).</summary>
    Multiplier,

    /// <summary>FSR 1, LS1, LS1 performance, SGSR, xBR, off (F6).</summary>
    Upscaler,

    /// <summary>The upscaler's sharpness or LS1's network (F7).</summary>
    Sharpness,

    /// <summary>Bars or stretched on a screen of another shape (F2).</summary>
    PictureFit,

    /// <summary>How strongly the pad rumbles: 200 %, 300 %, 100 %, 150 %.</summary>
    RumbleStrength,

    /// <summary>Fullscreen or a window (F11).</summary>
    Fullscreen,

    /// <summary>Start or stop a recording (F9).</summary>
    Record,

    /// <summary>Back to the game.</summary>
    Close,

    /// <summary>End the stream.</summary>
    EndStream,
}
