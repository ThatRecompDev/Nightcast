// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Linux.Events;

/// <summary>
/// The event types and codes of the kernel's input layer a pad is read with, as <c>linux/input-event-codes.h</c>
/// defines them (a stable kernel interface). Buttons keep the kernel's names: <c>BTN_X</c> and <c>BTN_NORTH</c> are
/// one code, which pads disagree on (<see cref="Mapping.FaceLayout"/>).
/// </summary>
internal static class EvdevCodes
{
    public const ushort Synchronize = 0x00;
    public const ushort Key = 0x01;
    public const ushort Absolute = 0x03;
    public const ushort ForceFeedback = 0x15;

    /// <summary><c>SYN_REPORT</c>: the events before it are one state of the pad.</summary>
    public const ushort Report = 0;

    /// <summary><c>SYN_DROPPED</c>: the kernel's buffer overran; events up to the next report are incomplete.</summary>
    public const ushort Dropped = 3;

    /// <summary><c>BTN_SOUTH</c> (<c>BTN_A</c>, <c>BTN_GAMEPAD</c>): the first gamepad button; a pad has it.</summary>
    public const ushort ButtonSouth = 0x130;
    public const ushort ButtonEast = 0x131;

    /// <summary><c>BTN_NORTH</c>, also <c>BTN_X</c>.</summary>
    public const ushort ButtonX = 0x133;

    /// <summary><c>BTN_WEST</c>, also <c>BTN_Y</c>.</summary>
    public const ushort ButtonY = 0x134;

    public const ushort ButtonLeftShoulder = 0x136;
    public const ushort ButtonRightShoulder = 0x137;
    public const ushort ButtonLeftTrigger = 0x138;
    public const ushort ButtonRightTrigger = 0x139;
    public const ushort ButtonSelect = 0x13A;
    public const ushort ButtonStart = 0x13B;
    public const ushort ButtonMode = 0x13C;
    public const ushort ButtonLeftThumb = 0x13D;
    public const ushort ButtonRightThumb = 0x13E;
    public const ushort ButtonDpadUp = 0x220;
    public const ushort ButtonDpadDown = 0x221;
    public const ushort ButtonDpadLeft = 0x222;
    public const ushort ButtonDpadRight = 0x223;

    /// <summary><c>KEY_MAX</c>: the highest key code, so a key bitmap is (KEY_MAX + 1) / 8 bytes.</summary>
    public const int KeyMax = 0x2FF;

    public const ushort AxisX = 0x00;
    public const ushort AxisY = 0x01;
    public const ushort AxisZ = 0x02;
    public const ushort AxisRx = 0x03;
    public const ushort AxisRy = 0x04;
    public const ushort AxisRz = 0x05;

    /// <summary><c>ABS_GAS</c> and <c>ABS_BRAKE</c>: the triggers of pads that do not use Z and RZ for them.</summary>
    public const ushort AxisGas = 0x09;
    public const ushort AxisBrake = 0x0A;

    public const ushort AxisHatX = 0x10;
    public const ushort AxisHatY = 0x11;

    /// <summary><c>ABS_CNT</c>: the number of absolute axis codes.</summary>
    public const int AxisCount = 0x40;

    /// <summary><c>FF_RUMBLE</c>: the two-motor effect, and the bit of it in the force-feedback bitmap.</summary>
    public const ushort Rumble = 0x50;

    /// <summary><c>FF_CNT</c>: the number of force-feedback codes.</summary>
    public const int ForceFeedbackCount = 0x80;

    /// <summary><c>EV_CNT</c>: the number of event types.</summary>
    public const int EventTypeCount = 0x20;

    /// <summary>Bits in a byte of a capability bitmap.</summary>
    public const int BitsPerByte = 8;
}
