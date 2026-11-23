// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Linux.Mapping;

/// <summary>
/// What a pad's device says of itself when it is found: its USB vendor and product (<c>EVIOCGID</c>), its absolute
/// axes by code (<c>EVIOCGABS</c>; <see cref="Axes"/> is <c>ABS_CNT</c> long, an axis it lacks the default) and
/// whether it plays a rumble effect (<c>FF_RUMBLE</c>). The family and the face layout follow from the vendor.
/// </summary>
internal sealed record EvdevDescription(ushort Vendor, ushort Product, EvdevAxisRange[] Axes, bool Rumble)
{
    /// <summary>Valve's vendor id: Steam Input's virtual pad (an Xbox 360 pad, product 0x11FF) and the Steam Deck's own.</summary>
    public const ushort Valve = 0x28DE;

    /// <summary>Whose pad it is: Steam Input's virtual pad is an Xbox pad; otherwise the engine's vendor table.</summary>
    public GamepadFamily Family
    {
        get
        {
            if (Vendor == Valve)
            {
                return GamepadFamily.Xbox;
            }

            return GamepadVendors.Family(Vendor);
        }
    }

    /// <summary>Sony's and Nintendo's kernel drivers send the face buttons by place, the rest as xpad does.</summary>
    public FaceLayout Layout
    {
        get
        {
            if (Vendor == GamepadVendors.Sony || Vendor == GamepadVendors.Nintendo)
            {
                return FaceLayout.Positional;
            }

            return FaceLayout.XboxLabels;
        }
    }

    /// <summary>What the pad offers through the engine's feedback.</summary>
    public GamepadFeatures Features
    {
        get
        {
            if (Rumble)
            {
                return GamepadFeatures.Rumble;
            }

            return GamepadFeatures.None;
        }
    }
}
