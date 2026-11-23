// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Linux.Mapping;

/// <summary>
/// What a pad's driver means by the kernel's <c>BTN_X</c>/<c>BTN_NORTH</c> (0x133) and <c>BTN_Y</c>/<c>BTN_WEST</c>
/// (0x134). The kernel's gamepad guide names them by place, but the Xbox drivers (xpad, and Steam Input's virtual
/// pad, which copies it) send the button labelled X as <c>BTN_X</c> and Y as <c>BTN_Y</c>, whatever their place.
/// </summary>
internal enum FaceLayout
{
    /// <summary>0x133 is the button labelled X (left, West) and 0x134 the one labelled Y (top, North): xpad, Steam Input.</summary>
    XboxLabels,

    /// <summary>0x133 is the top button (North) and 0x134 the left one (West): hid-playstation, hid-nintendo.</summary>
    Positional,
}
