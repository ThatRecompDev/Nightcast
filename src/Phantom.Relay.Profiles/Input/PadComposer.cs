// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Profiles.KeyboardMouse;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// Everything between the player's hands and the pad the console reads, one input frame at a time, under one
/// <see cref="StreamProfile"/>: the physical pad in <see cref="Slot"/> (<see cref="PhysicalPad"/>, under the profile's
/// <see cref="PadSettings"/>) or keyboard and mouse (<see cref="KeyboardMouseMapper"/>, the pad at rest unless the
/// profile switches them on). The console follows one of them, never a mix (<see cref="Source"/>): the controller while
/// it is used (<see cref="PadActivity"/>, past drift), keyboard and mouse from a fresh act on them
/// (<see cref="KeyboardMouseActivity"/>) until the controller is used again. The controller wins a frame where both
/// are, so a held key or a jittering mouse never locks it out, and a resting or drifting stick never fights the mouse. A window that lost focus has no keys or mouse buttons held (the engine lets them go); the mouse's
/// stick is centred at once. When to send it is <see cref="PadReportPolicy"/>'s. Made once per profile; a frame
/// allocates nothing.
/// </summary>
public sealed class PadComposer
{
    private readonly PhysicalPad _pad;
    private readonly KeyboardMouseMapper _keyboardMouse;
    private readonly bool _keyboardMouseEnabled;

    public PadComposer(StreamProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _pad = new PhysicalPad(profile.Pad);
        _keyboardMouse = new KeyboardMouseMapper(profile.KeyboardMouse);
        _keyboardMouseEnabled = profile.KeyboardMouse.Enabled;
    }

    /// <summary>The engine's pad slot played; null (the default) plays the first connected one.</summary>
    public int? Slot { get; set; }

    /// <summary>The hands the console's pad follows now.</summary>
    public PadSource Source { get; private set; }

    /// <summary>The console's pad for this input frame, <paramref name="elapsedSeconds"/> after the last.</summary>
    [HotPath]
    public XboxPadState Compose(InputFrame frame, float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (!frame.Focused)
        {
            _keyboardMouse.Reset();
        }

        var pad = _pad.Read(frame.Gamepads, Slot);
        var keyboardMouse = _keyboardMouse.Map(frame, elapsedSeconds);
        Follow(in pad, frame);

        if (Source == PadSource.KeyboardMouse)
        {
            return keyboardMouse;
        }

        return pad;
    }

    /// <summary>
    /// A controller in use takes the pad (it wins a frame where both are used); otherwise a fresh act on keyboard or
    /// mouse does, when the profile plays them. Neither leaves the source as it was.
    /// </summary>
    [HotPath]
    private void Follow(in XboxPadState pad, InputFrame frame)
    {
        if (PadActivity.IsUsed(in pad, PadActivity.ControllerThreshold))
        {
            Source = PadSource.Controller;
        }
        else if (_keyboardMouseEnabled && KeyboardMouseActivity.Acted(frame))
        {
            Source = PadSource.KeyboardMouse;
        }
    }
}
