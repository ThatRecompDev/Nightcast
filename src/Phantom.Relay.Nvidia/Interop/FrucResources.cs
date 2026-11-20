// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.CompilerServices;

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>The pArrResource array of the (un)register parameters: NvOFFRUC_MAX_RESOURCE texture pointers.</summary>
[InlineArray(FrucValues.MaxResources)]
internal struct FrucResources
{
    private nint _first;
}
