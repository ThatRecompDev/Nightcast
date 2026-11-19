// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// Finds a GPU by its LUID, so the decoder's device sits on the same adapter as the renderer's: a texture is shared only
/// between devices of one adapter, and a PC with an integrated GPU beside a discrete one has two.
/// </summary>
internal static class DxgiAdapters
{
    /// <summary>The adapter of <paramref name="luid"/> (a reference the caller releases); 0 and why when there is none.</summary>
    public static nint Find(long luid, out string? problem)
    {
        var result = NativeMethods.CreateDXGIFactory1(in SharingCalls.DxgiFactory4, out var factory);

        if (ComObjects.Failed(result))
        {
            problem = $"CreateDXGIFactory1 for IDXGIFactory4 failed (0x{result:X8})";

            return 0;
        }

        result = SharingCalls.EnumAdapterByLuid(factory, luid, out var adapter);
        ComObjects.Release(ref factory);

        if (ComObjects.Failed(result))
        {
            problem = $"no DXGI adapter has the renderer's LUID 0x{luid:X} (0x{result:X8})";

            return 0;
        }

        problem = null;

        return adapter;
    }
}
