// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

/// <summary>The structs the layer check hands the loader are laid out as gcc lays out Vulkan's own on x86-64 (sizes printed by a C build of the same check).</summary>
public sealed class VulkanLayoutTests
{
    [Fact]
    public unsafe void The_create_info_the_layer_properties_and_lsfg_vks_version_query_have_Cs_sizes_and_offsets()
    {
        Assert.Equal(64, sizeof(VulkanInstanceCreateInfo));
        Assert.Equal(520, sizeof(VulkanLayerProperties));
        Assert.Equal(24, sizeof(LsfgVersionInfo));
        Assert.Equal(8, Marshal.OffsetOf<VulkanInstanceCreateInfo>(nameof(VulkanInstanceCreateInfo.Next)).ToInt32());
        Assert.Equal(56, Marshal.OffsetOf<VulkanInstanceCreateInfo>(nameof(VulkanInstanceCreateInfo.EnabledExtensionNames)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<LsfgVersionInfo>(nameof(LsfgVersionInfo.Version)).ToInt32());
    }
}
