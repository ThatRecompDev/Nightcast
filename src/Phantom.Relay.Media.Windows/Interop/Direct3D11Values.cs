// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// The numbers and interface ids of D3D11 and its video side that the compute passes and the video processor use,
/// each as the Windows SDK header (d3d11.h, dxgiformat.h, d3dcommon.h) names it. The decoder's own few live in
/// <see cref="MediaFoundationValues"/>.
/// </summary>
internal static class Direct3D11Values
{
    /// <summary>DXGI_FORMAT_R8G8B8A8_UNORM.</summary>
    public const uint Rgba8Format = 28;

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM.</summary>
    public const uint Bgra8Format = 87;

    /// <summary>DXGI_FORMAT_R16G16B16A16_FLOAT.</summary>
    public const uint Rgba16FloatFormat = 10;

    /// <summary>DXGI_FORMAT_R8_UNORM.</summary>
    public const uint R8Format = 61;

    /// <summary>D3D11_USAGE_DEFAULT: GPU read and write.</summary>
    public const uint DefaultUsage = 0;

    /// <summary>D3D11_USAGE_IMMUTABLE: GPU read only, contents given at creation.</summary>
    public const uint ImmutableUsage = 1;

    /// <summary>D3D11_BIND_CONSTANT_BUFFER.</summary>
    public const uint BindConstantBuffer = 0x4;

    /// <summary>D3D11_BIND_SHADER_RESOURCE.</summary>
    public const uint BindShaderResource = 0x8;

    /// <summary>D3D11_BIND_RENDER_TARGET: a video processor's output must be one.</summary>
    public const uint BindRenderTarget = 0x20;

    /// <summary>D3D11_BIND_UNORDERED_ACCESS.</summary>
    public const uint BindUnorderedAccess = 0x80;

    /// <summary>D3D11_BIND_DECODER: a decoder's output surface, which a video processor accepts as input.</summary>
    public const uint BindDecoder = 0x200;

    /// <summary>D3D_DRIVER_TYPE_UNKNOWN (d3dcommon.h): the driver of the adapter given to D3D11CreateDevice.</summary>
    public const int UnknownDriver = 0;

    /// <summary>D3D11_RESOURCE_MISC_SHARED: a texture another device may open.</summary>
    public const uint MiscShared = 0x2;

    /// <summary>D3D11_RESOURCE_MISC_SHARED_NTHANDLE: shared through an NT handle (IDXGIResource1::CreateSharedHandle).</summary>
    public const uint MiscSharedNtHandle = 0x800;

    /// <summary>D3D11_SRV_DIMENSION_TEXTURE2D.</summary>
    public const uint ShaderResourceTexture2D = 4;

    /// <summary>D3D11_UAV_DIMENSION_TEXTURE2D.</summary>
    public const uint UnorderedAccessTexture2D = 4;

    /// <summary>D3D11_FILTER_MIN_MAG_MIP_LINEAR.</summary>
    public const uint LinearFilter = 0x15;

    /// <summary>D3D11_TEXTURE_ADDRESS_CLAMP.</summary>
    public const uint ClampAddress = 3;

    /// <summary>D3D11_COMPARISON_NEVER.</summary>
    public const uint ComparisonNever = 1;

    /// <summary>D3D11_FLOAT32_MAX: no upper limit on the mip level a sampler reads.</summary>
    public const float MaxLod = float.MaxValue;

    /// <summary>D3D11_QUERY_TIMESTAMP: the GPU clock when the query ends.</summary>
    public const uint TimestampQuery = 2;

    /// <summary>D3D11_QUERY_TIMESTAMP_DISJOINT: the GPU clock's frequency over a bracket of timestamps.</summary>
    public const uint TimestampDisjointQuery = 3;

    /// <summary>S_FALSE: a query's data is not ready yet.</summary>
    public const int NotReady = 1;

    /// <summary>D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE.</summary>
    public const uint ProgressiveFrames = 0;

    /// <summary>D3D11_VIDEO_USAGE_OPTIMAL_SPEED.</summary>
    public const uint OptimalSpeedUsage = 1;

    /// <summary>D3D11_VPIV_DIMENSION_TEXTURE2D.</summary>
    public const uint VideoInputTexture2D = 1;

    /// <summary>D3D11_VPOV_DIMENSION_TEXTURE2D.</summary>
    public const uint VideoOutputTexture2D = 1;

    /// <summary>
    /// D3D11_VIDEO_PROCESSOR_COLOR_SPACE for the decoder's pictures: YCbCr_Matrix (bit 2) set for BT.709 and
    /// Nominal_Range (bits 4 and 5) at D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_16_235, the console's limited range.
    /// </summary>
    public const uint Bt709LimitedColourSpace = 0x14;

    /// <summary>D3D11_VIDEO_PROCESSOR_COLOR_SPACE for the RGBA output: RGB_Range (bit 1) clear, full 0-255.</summary>
    public const uint FullRangeRgbColourSpace = 0;

    /// <summary>IID_ID3D11VideoDevice.</summary>
    public static readonly Guid VideoDeviceInterface = new("10EC4D5B-975A-4689-B9E4-D0AAC30FE333");

    /// <summary>IID_ID3D11VideoContext.</summary>
    public static readonly Guid VideoContextInterface = new("61F21C45-3C0E-4a74-9CEA-67100D9AD5E4");
}
