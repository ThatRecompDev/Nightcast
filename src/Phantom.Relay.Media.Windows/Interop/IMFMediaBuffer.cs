// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>IMFMediaBuffer (mfobjects.h): a block of memory, locked for reading or writing.</summary>
[GeneratedComInterface]
[Guid("045FA593-8799-42b8-BC8D-8968C6453507")]
internal partial interface IMFMediaBuffer
{
    [PreserveSig]
    int Lock(out nint data, out uint maxLength, out uint currentLength);

    [PreserveSig]
    int Unlock();

    [PreserveSig]
    int GetCurrentLength(out uint length);

    [PreserveSig]
    int SetCurrentLength(uint length);

    [PreserveSig]
    int GetMaxLength(out uint length);
}
