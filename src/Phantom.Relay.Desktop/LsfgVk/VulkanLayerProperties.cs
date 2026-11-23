// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>Vulkan's <c>VkLayerProperties</c>: the layer's name, the versions and its description, as C lays them out.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VulkanLayerProperties
{
    /// <summary><c>VK_MAX_EXTENSION_NAME_SIZE</c> and <c>VK_MAX_DESCRIPTION_SIZE</c>.</summary>
    public const int NameSize = 256;
    public const int DescriptionSize = 256;

    public fixed byte LayerName[NameSize];
    public uint SpecVersion;
    public uint ImplementationVersion;
    public fixed byte Description[DescriptionSize];
}
