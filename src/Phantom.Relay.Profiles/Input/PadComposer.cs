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
/// profile switches them on). The console follows one of them, never a mix (<see cref="Source"/>): keyboard and mouse
/// from the moment they are used, the controller again from the moment it is (<see cref="PadActivity"/>), so a
/// connected pad's resting stick or a drifting one never fights the mouse. A frame in which both are used leaves the
/// source as it was. A window that lost focus has no keys or mouse buttons held (the engine lets them go); the mouse's
/// stick is centred at once. When to send it is <see cref="PadReportPolicy"/>'s. Made once per profile; a frame
/// allocates nothing.
/// </summary>
public sealed class PadComposer
{
    private readonly PhysicalPad _pad;
    private readonly KeyboardMouseMapper _keyboardMouse;

    public PadComposer(StreamProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _pad = new PhysicalPad(profile.Pad);
        _keyboardMouse = new KeyboardMouseMapper(profile.KeyboardMouse);
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
        Follow(in pad, in keyboardMouse);

        if (Source == PadSource.KeyboardMouse)
        {
            return keyboardMouse;
        }

        return pad;
    }

    /// <summary>The source switches to the hands used this frame, unless both or neither were.</summary>
    [HotPath]
    private void Follow(in XboxPadState pad, in XboxPadState keyboardMouse)
    {
        bool padUsed = PadActivity.IsUsed(in pad, PadActivity.ControllerThreshold);
        bool keyboardMouseUsed = PadActivity.IsUsed(in keyboardMouse, 0f);

        if (keyboardMouseUsed && !padUsed)
        {
            Source = PadSource.KeyboardMouse;
        }
        else if (padUsed && !keyboardMouseUsed)
        {
            Source = PadSource.Controller;
        }
    }
}
