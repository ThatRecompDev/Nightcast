// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>ID3D11Texture2D (d3d11.h): ID3D11DeviceChild's four slots, ID3D11Resource's three, then GetDesc.</summary>
[GeneratedComInterface]
[Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c")]
internal partial interface ID3D11Texture2D
{
    void GetDevice();

    void GetPrivateData();

    void SetPrivateData();

    void SetPrivateDataInterface();

    void GetResourceType();

    void SetEvictionPriority();

    void GetEvictionPriority();

    [PreserveSig]
    void GetDesc(out Texture2DDescription description);
}
