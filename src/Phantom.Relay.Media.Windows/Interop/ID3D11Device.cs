// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>ID3D11Device (d3d11.h), up to CreateTexture2D: the staging texture the decoded pictures are read through.</summary>
[GeneratedComInterface]
[Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
internal partial interface ID3D11Device
{
    void CreateBuffer();

    void CreateTexture1D();

    [PreserveSig]
    int CreateTexture2D(in Texture2DDescription description, nint initialData, out nint texture);
}
