// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_BOX (d3d11.h): the part of a texture a copy reads, right, bottom and back exclusive.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Box
{
    public uint Left;
    public uint Top;
    public uint Front;
    public uint Right;
    public uint Bottom;
    public uint Back;
}
