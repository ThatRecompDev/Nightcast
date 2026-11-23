// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The check through this process's own Vulkan loader, after the layer's variables are set and before the window's
/// instance exists: the loader's list of instance layers (the manifest found, not switched off), then a bare instance
/// made and destroyed at once, which loads the layer; whether it started and took Nightcast's profile is read from
/// what it wrote into its own log (<see cref="LsfgVkLog"/>) during that instance, not from a structure chained to it
/// (an earlier guess at lsfg-vk's version structure read a bad pointer and crashed). The layer, once loaded, stays
/// loaded in the process with the profile it took; the window's instance uses the same. The bare instance asks for no
/// surface, so lsfg-vk passes it through untouched.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed unsafe class VulkanLayerProbe : ILayerProbe
{
    private readonly string _logPath;

    /// <summary><paramref name="logPath"/> is the log lsfg-vk writes (the config's <c>log_file</c>).</summary>
    public VulkanLayerProbe(string logPath)
    {
        _logPath = logPath;
    }

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

    /// <summary>The version the layer logged while a bare instance was made; null when it did not start with Nightcast's profile.</summary>
    private string? LoadedVersion()
    {
        long before = LsfgVkLog.Length(_logPath);
        var info = new VulkanInstanceCreateInfo { StructureType = VulkanInstanceCreateInfo.Type };
        nint instance = 0;

        if (VulkanMethods.CreateInstance(&info, null, &instance) == VulkanMethods.Success)
        {
            VulkanMethods.DestroyInstance(instance, null);
        }

        return LsfgVkLog.LoadedVersion(LsfgVkLog.ReadFrom(_logPath, before));
    }
}
