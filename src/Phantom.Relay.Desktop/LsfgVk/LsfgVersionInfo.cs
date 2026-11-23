// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// lsfg-vk's <c>VkLSFGVersionInfo</c> (its <c>thirdparty/vk</c> header): chained to a <c>vkCreateInstance</c>, the
/// layer, when it loaded and took a profile, writes its version string into it (its own <c>lsfg-vk-cli healthcheck</c>
/// asks the same way). Left null, the layer did not load.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct LsfgVersionInfo
{
    /// <summary><c>VK_LSFGVK_STRUCTURE_TYPE_VERSION_INFO</c>: "LSFG" in ASCII.</summary>
    public const int Type = 0x4C534647;

    public int StructureType;
    public void* Next;
    public byte* Version;
}
