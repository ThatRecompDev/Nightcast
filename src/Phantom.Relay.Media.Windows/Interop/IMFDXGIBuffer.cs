// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// IMFDXGIBuffer (mfobjects.h): a media buffer that is a DXGI surface, a decoder's output texture (often one slice of
/// a texture array, its subresource index).
/// </summary>
[GeneratedComInterface]
[Guid("e7174cfa-1c9e-48b1-8866-626226bfc258")]
internal partial interface IMFDXGIBuffer
{
    [PreserveSig]
    int GetResource(in Guid interfaceId, out nint resource);

    [PreserveSig]
    int GetSubresourceIndex(out uint index);
}
