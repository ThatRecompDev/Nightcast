// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_QUERY_DESC (d3d11.h): the kind of query (a GPU timestamp, say) and its flags.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct QueryDescription
{
    public uint Query;
    public uint MiscFlags;
}
