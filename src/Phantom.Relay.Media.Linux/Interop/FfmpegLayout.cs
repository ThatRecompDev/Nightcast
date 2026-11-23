// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Linux.Interop;

/// <summary>
/// The few fields of FFmpeg's structs the decoder reads or writes, as byte offsets on 64-bit Linux, and the enum and
/// error values it uses, all for FFmpeg 8.1 (libavcodec 62, libavutil 60; <see cref="FfmpegLibraries"/> refuses any
/// other major). Measured with offsetof against the headers of the pinned build (scripts/build/build-ffmpeg-linux.ps1
/// copies them beside the libraries; docs/architecture/linux-decode.md has the program). FFmpeg only moves a public
/// field at a major version bump, which is why the major is checked at start.
/// </summary>
internal static class FfmpegLayout
{
    /// <summary>AVCodecContext.get_format: the callback that picks the picture format (VA-API surfaces).</summary>
    public const int CodecContextGetFormat = 192;

    /// <summary>AVCodecContext.hw_device_ctx: the AVBufferRef of the hardware device the decoder runs on.</summary>
    public const int CodecContextHwDevice = 560;

    /// <summary>AVPacket.pts.</summary>
    public const int PacketPts = 8;

    /// <summary>AVPacket.data.</summary>
    public const int PacketData = 24;

    /// <summary>AVPacket.size.</summary>
    public const int PacketSize = 32;

    /// <summary>AVFrame.data: eight plane pointers.</summary>
    public const int FrameData = 0;

    /// <summary>AVFrame.linesize: eight int pitches.</summary>
    public const int FrameLinesize = 64;

    /// <summary>AVFrame.width.</summary>
    public const int FrameWidth = 104;

    /// <summary>AVFrame.height.</summary>
    public const int FrameHeight = 108;

    /// <summary>AVFrame.format (an AVPixelFormat for video).</summary>
    public const int FrameFormat = 116;

    /// <summary>AVFrame.pts.</summary>
    public const int FramePts = 136;

    /// <summary>AVBufferRef.data.</summary>
    public const int BufferRefData = 8;

    /// <summary>AVHWDeviceContext.hwctx: the API's own device context (AVVAAPIDeviceContext).</summary>
    public const int DeviceContextHwContext = 16;

    /// <summary>AVVAAPIDeviceContext.display: the VADisplay.</summary>
    public const int VaapiDeviceDisplay = 0;

    /// <summary>AV_CODEC_ID_H264.</summary>
    public const int CodecIdH264 = 27;

    /// <summary>AV_HWDEVICE_TYPE_VAAPI.</summary>
    public const int DeviceTypeVaapi = 3;

    /// <summary>AV_PIX_FMT_NONE, which also ends get_format's list.</summary>
    public const int PixelFormatNone = -1;

    /// <summary>AV_PIX_FMT_YUV420P: three 8-bit planes, 4:2:0.</summary>
    public const int PixelFormatYuv420P = 0;

    /// <summary>AV_PIX_FMT_YUVJ420P: the same in full range (deprecated, still offered for full-range streams).</summary>
    public const int PixelFormatYuvj420P = 12;

    /// <summary>AV_PIX_FMT_NV12: a Y plane, then interleaved Cb, Cr pairs.</summary>
    public const int PixelFormatNv12 = 23;

    /// <summary>AV_PIX_FMT_VAAPI: a frame whose data[3] is a VASurfaceID.</summary>
    public const int PixelFormatVaapi = 44;

    /// <summary>AVERROR(EAGAIN) on Linux: send more input, or take the output first.</summary>
    public const int ErrorAgain = -11;

    /// <summary>AVERROR_EOF.</summary>
    public const int ErrorEndOfFile = -541478725;

    /// <summary>How many plane pointers and pitches an AVFrame has (AV_NUM_DATA_POINTERS).</summary>
    public const int DataPointers = 8;

    /// <summary>Room for av_strerror's text.</summary>
    public const int ErrorTextBytes = 128;
}
