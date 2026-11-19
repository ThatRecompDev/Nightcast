// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// ID3D10Multithread (d3d10.h): makes the device's immediate context safe to share with the decoder, which DXVA under
/// Media Foundation requires.
/// </summary>
[GeneratedComInterface]
[Guid("9B7E4E00-342C-4106-A19F-4F2704F689F0")]
internal partial interface ID3D10Multithread
{
    void Enter();

    void Leave();

    [PreserveSig]
    int SetMultithreadProtected(int protect);
}
