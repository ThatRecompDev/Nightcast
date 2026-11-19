// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// Raw COM pointers and their source-generated wrappers. A wrapper is made unique (never shared through the wrappers'
/// cache) and released at once when done (<see cref="Free"/>), so a pooled decoder sample is never kept alive by a
/// wrapper the garbage collector has not yet finalized; the raw reference stays the caller's to release.
/// </summary>
internal static class ComObjects
{
    /// <summary>A wrapper of <paramref name="pointer"/> as <typeparamref name="T"/>; it holds a reference of its own.</summary>
    public static T Wrap<T>(ComWrappers wrappers, nint pointer)
        where T : class
    {
        return (T)wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
    }

    /// <summary>Releases a wrapper's reference now rather than at finalization.</summary>
    public static void Free(object? wrapper)
    {
        if (wrapper is ComObject com)
        {
            com.FinalRelease();
        }
    }

    /// <summary>Releases a raw reference and clears it.</summary>
    public static void Release(ref nint pointer)
    {
        if (pointer == 0)
        {
            return;
        }

        _ = Marshal.Release(pointer);
        pointer = 0;
    }

    /// <summary>Whether an HRESULT is a failure.</summary>
    public static bool Failed(int result)
    {
        return result < 0;
    }
}
