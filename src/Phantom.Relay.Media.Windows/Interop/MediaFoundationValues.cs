// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>The numbers of Media Foundation, COM and D3D11 the decoder uses, each as the Windows SDK header names it.</summary>
internal static class MediaFoundationValues
{
    /// <summary>MF_VERSION (mfapi.h): MF_SDK_VERSION 2 in the high word, MF_API_VERSION 0x70 in the low.</summary>
    public const uint Version = 0x00020070;

    /// <summary>MFSTARTUP_FULL (mfapi.h).</summary>
    public const uint StartupFull = 0;

    /// <summary>COINIT_MULTITHREADED (objbase.h).</summary>
    public const uint MultithreadedApartment = 0;

    /// <summary>CLSCTX_INPROC_SERVER (wtypes.h).</summary>
    public const uint InProcessServer = 1;

    /// <summary>MF_E_TRANSFORM_NEED_MORE_INPUT (mferror.h): no picture until more input.</summary>
    public const int NeedMoreInput = unchecked((int)0xC00D6D72);

    /// <summary>MF_E_TRANSFORM_STREAM_CHANGE (mferror.h): the output format changed; choose an output type again.</summary>
    public const int StreamChange = unchecked((int)0xC00D6D61);

    /// <summary>MF_E_NOTACCEPTING (mferror.h): take the waiting output before giving more input.</summary>
    public const int NotAccepting = unchecked((int)0xC00D36B5);

    /// <summary>MFT_MESSAGE_COMMAND_FLUSH (mftransform.h).</summary>
    public const uint CommandFlush = 0;

    /// <summary>MFT_MESSAGE_COMMAND_DRAIN (mftransform.h).</summary>
    public const uint CommandDrain = 1;

    /// <summary>MFT_MESSAGE_SET_D3D_MANAGER (mftransform.h): the parameter is an IMFDXGIDeviceManager, or 0.</summary>
    public const uint SetD3DManager = 2;

    /// <summary>MFT_MESSAGE_NOTIFY_BEGIN_STREAMING (mftransform.h).</summary>
    public const uint NotifyBeginStreaming = 0x10000000;

    /// <summary>MFT_MESSAGE_NOTIFY_START_OF_STREAM (mftransform.h).</summary>
    public const uint NotifyStartOfStream = 0x10000003;

    /// <summary>MFT_OUTPUT_STREAM_PROVIDES_SAMPLES (mftransform.h).</summary>
    public const uint ProvidesSamples = 0x100;

    /// <summary>MFT_OUTPUT_STREAM_CAN_PROVIDE_SAMPLES (mftransform.h).</summary>
    public const uint CanProvideSamples = 0x200;

    /// <summary>MFVideoInterlace_Progressive (mfobjects.h).</summary>
    public const uint Progressive = 2;

    /// <summary>D3D_DRIVER_TYPE_HARDWARE (d3dcommon.h).</summary>
    public const int HardwareDriver = 1;

    /// <summary>D3D11_CREATE_DEVICE_VIDEO_SUPPORT (d3d11.h): a device DXVA can decode on.</summary>
    public const uint VideoSupport = 0x800;

    /// <summary>D3D11_SDK_VERSION (d3d11.h).</summary>
    public const uint D3D11SdkVersion = 7;

    /// <summary>DXGI_FORMAT_NV12 (dxgiformat.h).</summary>
    public const uint Nv12Format = 103;

    /// <summary>D3D11_USAGE_STAGING (d3d11.h): a texture the CPU reads.</summary>
    public const uint StagingUsage = 3;

    /// <summary>D3D11_CPU_ACCESS_READ (d3d11.h).</summary>
    public const uint CpuRead = 0x20000;

    /// <summary>D3D11_MAP_READ (d3d11.h).</summary>
    public const uint MapRead = 1;

    /// <summary>Media Foundation's time unit: 100 nanoseconds.</summary>
    public const long NanosecondsPerUnit = 100;

    /// <summary>Media Foundation's time units in a second.</summary>
    public const long UnitsPerSecond = 10_000_000;

    /// <summary>MF_SOURCE_READER_FIRST_VIDEO_STREAM (mfreadwrite.h).</summary>
    public const uint FirstVideoStream = 0xFFFFFFFC;

    /// <summary>MF_SOURCE_READER_ALL_STREAMS (mfreadwrite.h).</summary>
    public const uint AllStreams = 0xFFFFFFFE;

    /// <summary>MF_SOURCE_READER_MEDIASOURCE (mfreadwrite.h): the source as a whole, for its presentation attributes.</summary>
    public const uint MediaSource = 0xFFFFFFFF;

    /// <summary>MF_SOURCE_READERF_ENDOFSTREAM (mfreadwrite.h).</summary>
    public const uint EndOfStream = 0x2;

    /// <summary>MFNominalRange_16_235 (mfobjects.h): limited range.</summary>
    public const uint LimitedRange = 2;

    /// <summary>MFVideoTransferMatrix_BT709 (mfobjects.h).</summary>
    public const uint Bt709Matrix = 1;

    /// <summary>MFVideoPrimaries_BT709 (mfobjects.h).</summary>
    public const uint Bt709Primaries = 2;

    /// <summary>MFVideoTransFunc_709 (mfobjects.h).</summary>
    public const uint Bt709Transfer = 5;

    /// <summary>eAVEncCommonRateControlMode_UnconstrainedVBR (codecapi.h): the mean bitrate, no peak limit.</summary>
    public const uint UnconstrainedVbr = 2;

    /// <summary>eAVEncH264VProfile_High (codecapi.h).</summary>
    public const uint H264HighProfile = 100;

    /// <summary>eAVEncH265VProfile_Main_420_8 (codecapi.h).</summary>
    public const uint HevcMainProfile = 1;
}
