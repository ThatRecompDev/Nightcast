// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>
/// The numbers and names of NVIDIA's FRUC library as its header (NvOFFRUC.h, Optical Flow SDK 5.0) names them, and the
/// files it ships as. None of the SDK's files is in this repository; these are hand-written from the header.
/// </summary>
internal static class FrucValues
{
    /// <summary>The library, in the SDK's NvOFFRUC\NvOFFRUCSample\bin\win64.</summary>
    public const string LibraryFile = "NvOFFRUC.dll";

    /// <summary>The CUDA 11 runtime the library imports, shipped beside it.</summary>
    public const string CudaRuntimeFile = "cudart64_110.dll";

    /// <summary>The exports (CreateProcName and the others).</summary>
    public const string CreateName = "NvOFFRUCCreate";

    public const string RegisterName = "NvOFFRUCRegisterResource";

    public const string UnregisterName = "NvOFFRUCUnregisterResource";

    public const string ProcessName = "NvOFFRUCProcess";

    public const string DestroyName = "NvOFFRUCDestroy";

    /// <summary>uiReserved[32] at the end of the parameter structures.</summary>
    public const int ReservedWords = 32;

    /// <summary>NvOFFRUC_MAX_RESOURCE: the most textures one registration holds.</summary>
    public const int MaxResources = 10;

    /// <summary>NvOFFRUC_MIN_RESOURCE: the fewest registered before the first NvOFFRUCProcess.</summary>
    public const int MinResources = 3;

    /// <summary>NvOFFRUCResourceType.DirectX11Resource.</summary>
    public const int DirectX11Resource = 1;

    /// <summary>NvOFFRUCSurfaceFormat.NV12Surface.</summary>
    public const int Nv12Surface = 0;

    /// <summary>NvOFFRUCSurfaceFormat.ARGBSurface (an R8G8B8A8_UNORM texture, as the SDK's sample makes it).</summary>
    public const int ArgbSurface = 1;

    /// <summary>NvOFFRUCCUDAResourceType.CudaResourceTypeUndefined: no CUDA resources with DirectX.</summary>
    public const int CudaResourceUndefined = -1;

    /// <summary>NvOFFRUC_SUCCESS.</summary>
    public const int Success = 0;
}
