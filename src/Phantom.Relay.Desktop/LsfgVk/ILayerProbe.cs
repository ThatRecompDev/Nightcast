// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>Asks the Vulkan loader of this process whether lsfg-vk's layer is there and loads.</summary>
public interface ILayerProbe
{
    /// <summary>Whether the loader lists lsfg-vk's layer, and the version the layer reports once loaded.</summary>
    LayerCheck Check();
}
