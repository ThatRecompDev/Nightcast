// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>IMFSinkWriterEx (mfreadwrite.h): the transforms a sink writer put on a stream (to name its encoder).</summary>
[GeneratedComInterface]
[Guid("588d72ab-5Bc1-496a-8714-b70617141b25")]
internal partial interface IMFSinkWriterEx : IMFSinkWriter
{
    [PreserveSig]
    int GetTransformForStream(uint streamIndex, uint transformIndex, out Guid category, out nint transform);
}
