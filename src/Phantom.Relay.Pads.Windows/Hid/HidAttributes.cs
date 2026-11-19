// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Pads.Windows.Hid;

/// <summary><c>HIDD_ATTRIBUTES</c>: its own size (set before the call), then the device's USB ids and version.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HidAttributes
{
    public uint Size;
    public ushort VendorId;
    public ushort ProductId;
    public ushort VersionNumber;
}
