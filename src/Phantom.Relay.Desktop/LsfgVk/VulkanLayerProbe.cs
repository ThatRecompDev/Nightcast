// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The check through this process's own Vulkan loader, after the layer's variables are set and before the window's
/// instance exists: the loader's list of instance layers (the manifest found, not switched off), then a bare instance
/// made with lsfg-vk's version query chained (<see cref="LsfgVersionInfo"/>) and destroyed at once. The layer, once
/// loaded, stays loaded in the process with the profile it took; the window's instance uses the same. The bare
/// instance asks for no surface, so lsfg-vk passes it through untouched.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed unsafe class VulkanLayerProbe : ILayerProbe
{
    public LayerCheck Check()
    {
        try
        {
            if (!Listed())
            {
                return new LayerCheck(false, null, null);
            }

            return new LayerCheck(true, LoadedVersion(), null);
        }
        catch (DllNotFoundException)
        {
            return new LayerCheck(false, null, "no Vulkan loader (libvulkan.so.1)");
        }
        catch (EntryPointNotFoundException)
        {
            return new LayerCheck(false, null, "the Vulkan loader lacks its instance calls");
        }
    }

    /// <summary>Whether the loader lists <see cref="LsfgVkRelease.LayerName"/> among the instance layers.</summary>
    private static bool Listed()
    {
        uint count = 0;

        if (VulkanMethods.EnumerateInstanceLayerProperties(&count, null) != VulkanMethods.Success || count == 0)
        {
            return false;
        }

        var layers = new VulkanLayerProperties[count];
        int result;

        fixed (VulkanLayerProperties* first = layers)
        {
            result = VulkanMethods.EnumerateInstanceLayerProperties(&count, first);
        }

        if (result is not VulkanMethods.Success and not VulkanMethods.Incomplete)
        {
            return false;
        }

        return Names(layers, count).Contains(LsfgVkRelease.LayerName, StringComparer.Ordinal);
    }

    private static List<string> Names(VulkanLayerProperties[] layers, uint count)
    {
        var names = new List<string>();

        for (var index = 0; index < count; index++)
        {
            fixed (byte* name = layers[index].LayerName)
            {
                names.Add(Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(name)));
            }
        }

        return names;
    }

    /// <summary>The version the layer wrote into a bare instance's creation; null when it did not.</summary>
    private static string? LoadedVersion()
    {
        var version = new LsfgVersionInfo { StructureType = LsfgVersionInfo.Type };
        var info = new VulkanInstanceCreateInfo { StructureType = VulkanInstanceCreateInfo.Type, Next = &version };
        nint instance = 0;

        if (VulkanMethods.CreateInstance(&info, null, &instance) == VulkanMethods.Success)
        {
            VulkanMethods.DestroyInstance(instance, null);
        }

        if (version.Version is null)
        {
            return null;
        }

        return Marshal.PtrToStringUTF8((nint)version.Version);
    }
}
