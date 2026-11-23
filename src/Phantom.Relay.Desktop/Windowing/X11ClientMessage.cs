// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>
/// Xlib's XClientMessageEvent on 64-bit Linux, inside the size of the XEvent union (24 longs) that XSendEvent reads:
/// type, serial, send_event, display, window, message_type, format, then five longs of data.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct X11ClientMessage
{
    /// <summary>ClientMessage.</summary>
    public const int ClientMessageType = 33;

    /// <summary>The data are longs.</summary>
    public const int LongFormat = 32;

    [FieldOffset(0)]
    public int Type;

    [FieldOffset(16)]
    public int SendEvent;

    [FieldOffset(24)]
    public nint Display;

    [FieldOffset(32)]
    public nint Window;

    [FieldOffset(40)]
    public nint MessageType;

    [FieldOffset(48)]
    public int Format;

    [FieldOffset(56)]
    public nint Data0;

    [FieldOffset(64)]
    public nint Data1;

    [FieldOffset(72)]
    public nint Data2;

    [FieldOffset(80)]
    public nint Data3;
}
