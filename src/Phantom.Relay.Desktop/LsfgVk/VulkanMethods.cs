// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>The Vulkan loader's calls the layer check needs, from <c>libvulkan.so.1</c> (the loader every Linux Vulkan driver installs).</summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class VulkanMethods
{
    /// <summary><c>VK_SUCCESS</c>, and <c>VK_INCOMPLETE</c> (more layers than room, for a list that grew between the calls).</summary>
    public const int Success = 0;
    public const int Incomplete = 5;

    private const string Library = "libvulkan.so.1";

    [LibraryImport(Library, EntryPoint = "vkEnumerateInstanceLayerProperties")]
    public static partial int EnumerateInstanceLayerProperties(uint* count, VulkanLayerProperties* properties);

    [LibraryImport(Library, EntryPoint = "vkCreateInstance")]
    public static partial int CreateInstance(VulkanInstanceCreateInfo* info, void* allocator, nint* instance);

    [LibraryImport(Library, EntryPoint = "vkDestroyInstance")]
    public static partial void DestroyInstance(nint instance, void* allocator);
}
