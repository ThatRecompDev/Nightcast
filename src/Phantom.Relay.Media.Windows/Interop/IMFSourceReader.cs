// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// IMFSourceReader (mfreadwrite.h): reads a media file's samples, as stored (the native type) or decoded to a type
/// set on the stream. Every slot in the header's order; the ones not called are declared without parameters.
/// </summary>
[GeneratedComInterface]
[Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993")]
internal unsafe partial interface IMFSourceReader
{
    void GetStreamSelection();

    [PreserveSig]
    int SetStreamSelection(uint streamIndex, int selected);

    [PreserveSig]
    int GetNativeMediaType(uint streamIndex, uint typeIndex, out nint type);

    [PreserveSig]
    int GetCurrentMediaType(uint streamIndex, out nint type);

    [PreserveSig]
    int SetCurrentMediaType(uint streamIndex, nint reserved, nint type);

    void SetCurrentPosition();

    [PreserveSig]
    int ReadSample(uint streamIndex, uint controlFlags, out uint actualStream, out uint streamFlags, out long timestamp, out nint sample);

    void Flush();

    void GetServiceForStream();

    [PreserveSig]
    int GetPresentationAttribute(uint streamIndex, in Guid attribute, ComVariant* value);
}
