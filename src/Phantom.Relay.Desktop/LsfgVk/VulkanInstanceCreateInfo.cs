// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>Vulkan's <c>VkInstanceCreateInfo</c>, as C lays it out (64 bytes on x86-64): no application info, layers or extensions.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct VulkanInstanceCreateInfo
{
    /// <summary><c>VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO</c>.</summary>
    public const int Type = 1;

    public int StructureType;
    public void* Next;
    public uint Flags;
    public void* ApplicationInfo;
    public uint EnabledLayerCount;
    public byte** EnabledLayerNames;
    public uint EnabledExtensionCount;
    public byte** EnabledExtensionNames;
}
