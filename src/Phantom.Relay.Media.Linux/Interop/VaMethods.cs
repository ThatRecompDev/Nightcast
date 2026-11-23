// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Linux.Interop;

/// <summary>The one libva call the decoder makes itself: the VA driver's own name, for the console line.</summary>
internal static partial class VaMethods
{
    [LibraryImport(FfmpegLibraries.Va, EntryPoint = "vaQueryVendorString")]
    public static partial nint QueryVendorString(nint display);
}
