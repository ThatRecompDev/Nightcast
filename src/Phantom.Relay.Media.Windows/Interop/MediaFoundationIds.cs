// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>The GUIDs of Media Foundation, the codec API and D3D11 the decoder uses, each as the Windows SDK header names it.</summary>
internal static class MediaFoundationIds
{
    /// <summary>CLSID_CMSH264DecoderMFT (wmcodecdsp.h): Windows' H.264 decoder, DXVA-accelerated when given a device.</summary>
    public static readonly Guid H264Decoder = new("62CE7E72-4C71-4d20-B15D-452831A87D9D");

    /// <summary>CLSID_CMSH264EncoderMFT (wmcodecdsp.h): Windows' software H.264 encoder (the Live test's stream).</summary>
    public static readonly Guid H264Encoder = new("6ca50344-051a-4ded-9779-a43305165e35");

    /// <summary>MFMediaType_Video (mfapi.h).</summary>
    public static readonly Guid VideoMajorType = new("73646976-0000-0010-8000-00AA00389B71");

    /// <summary>MFVideoFormat_H264 (mfapi.h): FOURCC H264.</summary>
    public static readonly Guid H264Subtype = new("34363248-0000-0010-8000-00AA00389B71");

    /// <summary>MFVideoFormat_NV12 (mfapi.h): FOURCC NV12, a Y plane then interleaved Cb, Cr.</summary>
    public static readonly Guid Nv12Subtype = new("3231564E-0000-0010-8000-00AA00389B71");

    /// <summary>MF_MT_MAJOR_TYPE (mfapi.h).</summary>
    public static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");

    /// <summary>MF_MT_SUBTYPE (mfapi.h).</summary>
    public static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

    /// <summary>MF_MT_FRAME_SIZE (mfapi.h): width in the high 32 bits, height in the low.</summary>
    public static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");

    /// <summary>MF_MT_FRAME_RATE (mfapi.h): numerator high, denominator low.</summary>
    public static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");

    /// <summary>MF_MT_PIXEL_ASPECT_RATIO (mfapi.h).</summary>
    public static readonly Guid PixelAspectRatio = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");

    /// <summary>MF_MT_INTERLACE_MODE (mfapi.h).</summary>
    public static readonly Guid InterlaceMode = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");

    /// <summary>MF_MT_MINIMUM_DISPLAY_APERTURE (mfapi.h): the shown area of a larger coded picture, an MFVideoArea.</summary>
    public static readonly Guid MinimumDisplayAperture = new("d7388766-18fe-48c6-a177-ee894867c8c4");

    /// <summary>MF_MT_AVG_BITRATE (mfapi.h).</summary>
    public static readonly Guid AverageBitrate = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");

    /// <summary>MF_MT_DEFAULT_STRIDE (mfapi.h): the bytes between a plane's rows in a system-memory buffer.</summary>
    public static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");

    /// <summary>MF_MT_MPEG2_PROFILE (mfapi.h): the H.264 profile an encoder writes.</summary>
    public static readonly Guid Mpeg2Profile = new("ad76a80b-2d5c-4e0b-b375-64e520137036");

    /// <summary>MF_MT_MPEG_SEQUENCE_HEADER (mfapi.h): an encoder's parameter sets in Annex-B form.</summary>
    public static readonly Guid MpegSequenceHeader = new("3c036de7-3ad0-4c9e-9216-ee6d6ac21cb3");

    /// <summary>
    /// MF_LOW_LATENCY (mfapi.h), the same GUID as CODECAPI_AVLowLatencyMode (codecapi.h): a decoder emits each
    /// picture as soon as it is decoded instead of filling its reorder queue first.
    /// </summary>
    public static readonly Guid LowLatency = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");

    /// <summary>MF_SA_D3D11_AWARE (mftransform.h): the transform can decode through a D3D11 device.</summary>
    public static readonly Guid D3D11Aware = new("206b4fc8-fcf9-4c51-afe3-9764369e33a0");

    /// <summary>IID_IMFTransform.</summary>
    public static readonly Guid TransformInterface = new("bf94c121-5b05-4e6f-8000-ba598961414d");

    /// <summary>IID_IMFDXGIBuffer.</summary>
    public static readonly Guid DxgiBufferInterface = new("e7174cfa-1c9e-48b1-8866-626226bfc258");

    /// <summary>IID_ICodecAPI.</summary>
    public static readonly Guid CodecApiInterface = new("901db4c7-31ce-41a2-85dc-8fa0bf41b8da");

    /// <summary>IID_ID3D11Texture2D.</summary>
    public static readonly Guid Texture2DInterface = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    /// <summary>IID_ID3D10Multithread.</summary>
    public static readonly Guid MultithreadInterface = new("9B7E4E00-342C-4106-A19F-4F2704F689F0");

    /// <summary>MFVideoFormat_HEVC (mfapi.h): FOURCC HEVC.</summary>
    public static readonly Guid HevcSubtype = new("43564548-0000-0010-8000-00AA00389B71");

    /// <summary>MF_MT_MPEG2_LEVEL (mfapi.h): the H.264 level, as level_idc.</summary>
    public static readonly Guid Mpeg2Level = new("96f66574-11c5-4015-8666-bff516436da7");

    /// <summary>MF_MT_VIDEO_NOMINAL_RANGE (mfapi.h): limited (16-235) or full range.</summary>
    public static readonly Guid NominalRange = new("c21b8ee5-b956-4071-8daf-325edf5cab11");

    /// <summary>MF_MT_YUV_MATRIX (mfapi.h).</summary>
    public static readonly Guid YuvMatrix = new("3e23d450-2c75-4d25-a00e-b91670d12327");

    /// <summary>MF_MT_VIDEO_PRIMARIES (mfapi.h).</summary>
    public static readonly Guid VideoPrimaries = new("dbfbe4d7-0740-4ee0-8192-850ab0e21935");

    /// <summary>MF_MT_TRANSFER_FUNCTION (mfapi.h).</summary>
    public static readonly Guid TransferFunction = new("5fb0fce9-be5c-4935-a811-ec838f8eed93");

    /// <summary>MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS (mfreadwrite.h): a sink writer may use a hardware encoder.</summary>
    public static readonly Guid EnableHardwareTransforms = new("a634a91c-822b-41b9-a494-4de4643612b0");

    /// <summary>MF_SINK_WRITER_D3D_MANAGER (mfreadwrite.h): the IMFDXGIDeviceManager a sink writer's encoder uses.</summary>
    public static readonly Guid SinkWriterDeviceManager = new("ec822da2-e1e9-4b29-a0d8-563c719f5269");

    /// <summary>MFSampleExtension_CleanPoint (mfapi.h): the sample is a key frame.</summary>
    public static readonly Guid CleanPoint = new("9cdf01d8-a0f0-43ba-b077-eaa06cbd728a");

    /// <summary>MF_PD_DURATION (mfidl.h): a presentation's length, 100 ns units as a VT_UI8.</summary>
    public static readonly Guid PresentationDuration = new("6c990d33-bb8e-477a-8598-0d5d96fcd88a");

    /// <summary>CODECAPI_AVEncCommonRateControlMode (codecapi.h).</summary>
    public static readonly Guid RateControlMode = new("1c0608e9-370c-4710-8a58-cb6181c42423");

    /// <summary>CODECAPI_AVEncCommonMeanBitRate (codecapi.h): bits per second.</summary>
    public static readonly Guid MeanBitRate = new("f7222374-2144-4815-b550-a37f8e12ee52");

    /// <summary>MFT_CATEGORY_VIDEO_ENCODER (mfapi.h).</summary>
    public static readonly Guid VideoEncoderCategory = new("f79eac7d-e545-4387-bdee-d647d7bde42a");

    /// <summary>MFT_FRIENDLY_NAME_Attribute (mftransform.h): a transform's name ("NVIDIA H.264 Encoder MFT").</summary>
    public static readonly Guid FriendlyName = new("314ffbae-5b41-4c95-9c19-4e7d586face3");

    /// <summary>MFT_ENUM_HARDWARE_URL_Attribute (mftransform.h): set on hardware transforms only.</summary>
    public static readonly Guid HardwareUrl = new("2fb866ac-b078-4942-ab6c-003d05cda674");
}
