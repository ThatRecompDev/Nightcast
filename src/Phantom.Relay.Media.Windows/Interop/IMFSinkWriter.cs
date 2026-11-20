// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// IMFSinkWriter (mfreadwrite.h): writes samples into a file (an MP4 for a .mp4 path), encoding them on the way when the
/// input type differs from the stream's type and passing them through when it is the same. Every slot in the header's
/// order; the ones not called are declared without parameters. Finalize is named <see cref="FinalizeWriting"/> here,
/// since C# keeps that name for destructors.
/// </summary>
[GeneratedComInterface]
[Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d")]
internal partial interface IMFSinkWriter
{
    [PreserveSig]
    int AddStream(nint targetType, out uint streamIndex);

    [PreserveSig]
    int SetInputMediaType(uint streamIndex, nint inputType, nint encodingParameters);

    [PreserveSig]
    int BeginWriting();

    [PreserveSig]
    int WriteSample(uint streamIndex, nint sample);

    void SendStreamTick();

    void PlaceMarker();

    void NotifyEndOfSegment();

    void Flush();

    [PreserveSig]
    int FinalizeWriting();

    void GetServiceForStream();

    void GetStatistics();
}
