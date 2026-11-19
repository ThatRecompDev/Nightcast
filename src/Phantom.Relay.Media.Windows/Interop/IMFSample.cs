// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>IMFSample (mfobjects.h), up to AddBuffer: a timed list of media buffers. Times are in 100 ns units.</summary>
[GeneratedComInterface]
[Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
internal unsafe partial interface IMFSample : IMFAttributes
{
    void GetSampleFlags();

    void SetSampleFlags();

    [PreserveSig]
    int GetSampleTime(out long time);

    [PreserveSig]
    int SetSampleTime(long time);

    void GetSampleDuration();

    [PreserveSig]
    int SetSampleDuration(long duration);

    void GetBufferCount();

    [PreserveSig]
    int GetBufferByIndex(uint index, out nint buffer);

    void ConvertToContiguousBuffer();

    [PreserveSig]
    int AddBuffer(nint buffer);
}
