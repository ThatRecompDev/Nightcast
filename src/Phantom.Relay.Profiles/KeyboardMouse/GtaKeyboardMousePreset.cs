// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>
/// A GTA-style layout, as the built-in profile "GTA VI (keyboard &amp; mouse)". The pad side follows the series' pad
/// layout as it stood in GTA V (GTA VI's own is not checked yet); the keys follow its PC layout:
/// <list type="table">
/// <item><term>W A S D</term><description>left stick; left Alt walks at half tilt; left Shift sprints (A)</description></item>
/// <item><term>Space</term><description>jump (X)</description></item>
/// <item><term>F</term><description>enter or leave a vehicle (Y)</description></item>
/// <item><term>left mouse / right mouse</term><description>shoot (RT) / aim (LT); aiming also slows the camera</description></item>
/// <item><term>R</term><description>reload (B)</description></item>
/// <item><term>Q</term><description>cover (RB)</description></item>
/// <item><term>Tab</term><description>weapon wheel (LB)</description></item>
/// <item><term>E</term><description>context action (D-pad right)</description></item>
/// <item><term>M</term><description>map (View)</description></item>
/// <item><term>Esc</term><description>pause (Menu)</description></item>
/// <item><term>C</term><description>look behind (right stick click)</description></item>
/// <item><term>mouse</term><description>right stick, through the curve below</description></item>
/// </list>
/// V (camera) is left unbound: the camera is the View button, which M already takes. The wheel is unbound: the pad has
/// no weapon cycling to give it. Every value is a const here, to tune in one place.
/// </summary>
public static class GtaKeyboardMousePreset
{
    public const string ProfileName = "GTA VI (keyboard & mouse)";

    /// <summary>Counts per second for full tilt: an 800 DPI mouse moved 2.5 inches a second turns at full speed.</summary>
    public const float FullTiltSpeed = MouseLookSettings.StandardFullTiltSpeed;

    /// <summary>Vertical turning a little slower than horizontal, so a sideways sweep does not drift the aim up or down.</summary>
    public const float SensitivityY = 0.9f;

    /// <summary>Lifts slow moves, partly undoing the game's own near-square stick curve.</summary>
    public const float Exponent = MouseLookSettings.StandardExponent;

    /// <summary>The camera's dead zone, estimated: GTA V's sat near a quarter of the travel; measure it in GTA VI.</summary>
    public const float AntiDeadZone = MouseLookSettings.StandardAntiDeadZone;

    /// <summary>One frame of smoothing: a mouse polled at 125 Hz against a 60 Hz stream still turns evenly.</summary>
    public const float SmoothingFrames = MouseLookSettings.StandardSmoothingFrames;

    /// <summary>Three frames at 60 Hz from full tilt to the centre.</summary>
    public const float DecaySeconds = MouseLookSettings.StandardDecaySeconds;

    /// <summary>Aiming (right mouse held) turns at 60 percent, for small corrections on the target.</summary>
    public const float AimMultiplier = MouseLookSettings.StandardAimMultiplier;

    /// <summary>Walking (left Alt held) tilts the stick halfway.</summary>
    public const float WalkTilt = MovementSettings.StandardWalkTilt;

    public static MouseLookSettings MouseLook { get; } = new(
        1f,
        SensitivityY,
        FullTiltSpeed,
        Exponent,
        AntiDeadZone,
        SmoothingFrames,
        DecaySeconds,
        AimMultiplier,
        PointerButton.Right,
        InvertY: false);

    public static KeyboardMouseSettings Settings { get; } = new(
        Enabled: true,
        MovementSettings.Wasd with { WalkTilt = WalkTilt },
        MouseLook,
        [
            new KeyBinding(Key.Space, PadControl.X),
            new KeyBinding(Key.F, PadControl.Y),
            new KeyBinding(Key.R, PadControl.B),
            new KeyBinding(Key.Q, PadControl.RightShoulder),
            new KeyBinding(Key.Tab, PadControl.LeftShoulder),
            new KeyBinding(Key.E, PadControl.DPadRight),
            new KeyBinding(Key.M, PadControl.View),
            new KeyBinding(Key.Escape, PadControl.Menu),
            new KeyBinding(Key.C, PadControl.RightThumb),
        ],
        [
            new MouseButtonBinding(PointerButton.Left, PadControl.RightTrigger),
            new MouseButtonBinding(PointerButton.Right, PadControl.LeftTrigger),
        ],
        WheelSettings.None);

    /// <summary>
    /// GTA VI's title id as the console names it in /streaming/properties/titleinfo: seen when the owner started the
    /// game during a live stream, 2026-11-19.
    /// </summary>
    public const string GtaSixTitleId = "7952987a";

    /// <summary>
    /// The profile, with the default stream settings and frame generation at x2 (GTA VI runs at 30 fps on the console,
    /// shown at 60), chosen whenever the console runs GTA VI.
    /// </summary>
    public static StreamProfile Profile { get; } = StreamProfile.Default with
    {
        Name = ProfileName,
        TitleIds = [GtaSixTitleId],
        FrameGeneration = FrameGeneration.QualityDoubled,
        KeyboardMouse = Settings,
    };
}
