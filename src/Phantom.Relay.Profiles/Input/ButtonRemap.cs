// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Profiles.Input;

/// <summary>
/// A profile's button remap (<see cref="PadSettings.Remap"/>) as a table made once: each button sent as the buttons
/// it is mapped to (none switches it off), an unlisted button as itself. Applying it allocates nothing.
/// </summary>
public sealed class ButtonRemap
{
    /// <summary>The bits of a console button set (<see cref="XboxButtons"/> is a 16-bit field).</summary>
    public const int ButtonBits = 16;

    private readonly XboxButtons[] _targets = new XboxButtons[ButtonBits];

    public ButtonRemap(IReadOnlyDictionary<XboxButtons, XboxButtons> remap)
    {
        ArgumentNullException.ThrowIfNull(remap);

        for (var bit = 0; bit < ButtonBits; bit++)
        {
            var button = (XboxButtons)(1 << bit);

            if (remap.TryGetValue(button, out var target))
            {
                _targets[bit] = target;
            }
            else
            {
                _targets[bit] = button;
            }
        }
    }

    /// <summary><paramref name="buttons"/>, each sent as its target.</summary>
    [HotPath]
    public XboxButtons Apply(XboxButtons buttons)
    {
        var sent = XboxButtons.None;

        for (var bit = 0; bit < ButtonBits; bit++)
        {
            if (((int)buttons & (1 << bit)) != 0)
            {
                sent |= _targets[bit];
            }
        }

        return sent;
    }
}
