// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// ID3D11VideoContext (d3d11.h), up to VideoProcessorBlt: ID3D11DeviceChild's four slots, then the header's order.
/// Only the colour spaces, the frame format, auto processing and the blit are called.
/// </summary>
[GeneratedComInterface]
[Guid("61F21C45-3C0E-4a74-9CEA-67100D9AD5E4")]
internal unsafe partial interface ID3D11VideoContext
{
    void GetDevice();

    void GetPrivateData();

    void SetPrivateData();

    void SetPrivateDataInterface();

    void GetDecoderBuffer();

    void ReleaseDecoderBuffer();

    void DecoderBeginFrame();

    void DecoderEndFrame();

    void SubmitDecoderBuffers();

    void DecoderExtension();

    void VideoProcessorSetOutputTargetRect();

    void VideoProcessorSetOutputBackgroundColor();

    [PreserveSig]
    void VideoProcessorSetOutputColorSpace(nint processor, in uint colourSpace);

    void VideoProcessorSetOutputAlphaFillMode();

    void VideoProcessorSetOutputConstriction();

    void VideoProcessorSetOutputStereoMode();

    void VideoProcessorSetOutputExtension();

    void VideoProcessorGetOutputTargetRect();

    void VideoProcessorGetOutputBackgroundColor();

    void VideoProcessorGetOutputColorSpace();

    void VideoProcessorGetOutputAlphaFillMode();

    void VideoProcessorGetOutputConstriction();

    void VideoProcessorGetOutputStereoMode();

    void VideoProcessorGetOutputExtension();

    [PreserveSig]
    void VideoProcessorSetStreamFrameFormat(nint processor, uint stream, uint format);

    [PreserveSig]
    void VideoProcessorSetStreamColorSpace(nint processor, uint stream, in uint colourSpace);

    void VideoProcessorSetStreamOutputRate();

    void VideoProcessorSetStreamSourceRect();

    void VideoProcessorSetStreamDestRect();

    void VideoProcessorSetStreamAlpha();

    void VideoProcessorSetStreamPalette();

    void VideoProcessorSetStreamPixelAspectRatio();

    void VideoProcessorSetStreamLumaKey();

    void VideoProcessorSetStreamStereoFormat();

    [PreserveSig]
    void VideoProcessorSetStreamAutoProcessingMode(nint processor, uint stream, int enable);

    void VideoProcessorSetStreamFilter();

    void VideoProcessorSetStreamExtension();

    void VideoProcessorGetStreamFrameFormat();

    void VideoProcessorGetStreamColorSpace();

    void VideoProcessorGetStreamOutputRate();

    void VideoProcessorGetStreamSourceRect();

    void VideoProcessorGetStreamDestRect();

    void VideoProcessorGetStreamAlpha();

    void VideoProcessorGetStreamPalette();

    void VideoProcessorGetStreamPixelAspectRatio();

    void VideoProcessorGetStreamLumaKey();

    void VideoProcessorGetStreamStereoFormat();

    void VideoProcessorGetStreamAutoProcessingMode();

    void VideoProcessorGetStreamFilter();

    void VideoProcessorGetStreamExtension();

    [PreserveSig]
    int VideoProcessorBlt(nint processor, nint outputView, uint outputFrame, uint streamCount, VideoProcessorStream* streams);
}
