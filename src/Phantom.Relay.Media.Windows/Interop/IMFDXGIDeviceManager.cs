// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>IMFDXGIDeviceManager (mfobjects.h), up to ResetDevice: shares a D3D11 device with a transform for DXVA.</summary>
[GeneratedComInterface]
[Guid("eb533d5d-2db6-40f8-97a9-494692014f07")]
internal partial interface IMFDXGIDeviceManager
{
    void CloseDeviceHandle();

    void GetVideoService();

    void LockDevice();

    void OpenDeviceHandle();

    [PreserveSig]
    int ResetDevice(nint device, uint resetToken);
}
