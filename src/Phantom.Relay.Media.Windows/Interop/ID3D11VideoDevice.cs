// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// ID3D11VideoDevice (d3d11.h), up to CreateVideoProcessorEnumerator: the device's video side, which makes the video
/// processor that turns a decoder's NV12 surface into RGBA without a shader. Slots in the header's order.
/// </summary>
[GeneratedComInterface]
[Guid("10EC4D5B-975A-4689-B9E4-D0AAC30FE333")]
internal partial interface ID3D11VideoDevice
{
    void CreateVideoDecoder();

    [PreserveSig]
    int CreateVideoProcessor(nint enumerator, uint rateConversionIndex, out nint processor);

    void CreateAuthenticatedChannel();

    void CreateCryptoSession();

    void CreateVideoDecoderOutputView();

    [PreserveSig]
    int CreateVideoProcessorInputView(nint resource, nint enumerator, in VideoProcessorInputViewDescription description, out nint view);

    [PreserveSig]
    int CreateVideoProcessorOutputView(nint resource, nint enumerator, in VideoProcessorOutputViewDescription description, out nint view);

    [PreserveSig]
    int CreateVideoProcessorEnumerator(in VideoProcessorContentDescription description, out nint enumerator);
}
