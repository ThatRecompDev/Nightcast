// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// IMFTransform (mftransform.h): a Media Foundation transform, here the H.264 decoder (and, in the tests, the
/// encoder). Media types and samples travel as <see cref="nint"/> pointers the caller owns.
/// </summary>
[GeneratedComInterface]
[Guid("bf94c121-5b05-4e6f-8000-ba598961414d")]
internal partial interface IMFTransform
{
    void GetStreamLimits();

    void GetStreamCount();

    void GetStreamIDs();

    void GetInputStreamInfo();

    [PreserveSig]
    int GetOutputStreamInfo(uint stream, out MftOutputStreamInfo info);

    [PreserveSig]
    int GetAttributes(out nint attributes);

    void GetInputStreamAttributes();

    void GetOutputStreamAttributes();

    void DeleteInputStream();

    void AddInputStreams();

    void GetInputAvailableType();

    [PreserveSig]
    int GetOutputAvailableType(uint stream, uint index, out nint type);

    [PreserveSig]
    int SetInputType(uint stream, nint type, uint flags);

    [PreserveSig]
    int SetOutputType(uint stream, nint type, uint flags);

    void GetInputCurrentType();

    [PreserveSig]
    int GetOutputCurrentType(uint stream, out nint type);

    void GetInputStatus();

    void GetOutputStatus();

    void SetOutputBounds();

    void ProcessEvent();

    [PreserveSig]
    int ProcessMessage(uint message, nuint parameter);

    [PreserveSig]
    int ProcessInput(uint stream, nint sample, uint flags);

    [PreserveSig]
    unsafe int ProcessOutput(uint flags, uint count, MftOutputDataBuffer* buffers, out uint status);
}
