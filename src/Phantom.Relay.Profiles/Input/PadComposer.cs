// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Profiles.KeyboardMouse;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// Everything between the player's hands and the pad the console reads, one input frame at a time, under one
/// <see cref="StreamProfile"/>: the physical pad in <see cref="Slot"/> (<see cref="PhysicalPad"/>, under the profile's
/// <see cref="PadSettings"/>) and keyboard and mouse (<see cref="KeyboardMouseMapper"/>, the pad at rest unless the
/// profile switches them on), merged so a player can mix them (<see cref="PadMerge"/>). A window that lost focus
/// has no keys or mouse buttons held (the engine lets them go); the mouse's stick is centred at once. When to send it
/// is the report policy's. Made once per profile; a frame allocates nothing.
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

        return PadMerge.Merge(in pad, in keyboardMouse);
    }
}
