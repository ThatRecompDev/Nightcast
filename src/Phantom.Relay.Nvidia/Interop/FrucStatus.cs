// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Nvidia.Interop;

/// <summary>NvOFFRUC_STATUS (NvOFFRUC.h) in words, for the log: the header's error names in order.</summary>
internal static class FrucStatus
{
    private static readonly string[] Names =
    [
        "success",
        "optical flow not supported on this GPU",
        "invalid pointer",
        "invalid parameter",
        "invalid handle",
        "out of system memory",
        "out of video memory",
        "OpenCV not available",
        "unimplemented",
        "optical flow failed",
        "resource already registered",
        "resource not registered",
        "calls out of order",
        "write to disk failed",
        "pipeline stage failed",
        "synchronisation write failed",
        "generic error",
    ];

    /// <summary>"invalid parameter (3)", or the bare number for one the header does not list.</summary>
    public static string Describe(int status)
    {
        if (status >= 0 && status < Names.Length)
        {
            return $"{Names[status]} ({status})";
        }

        return $"status {status}";
    }
}
