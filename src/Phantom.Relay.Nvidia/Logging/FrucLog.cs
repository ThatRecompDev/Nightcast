// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.Nvidia.Logging;

/// <summary>NVIDIA FRUC's log messages, source-generated so a disabled level costs nothing. Never a path.</summary>
public static partial class FrucLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "NVIDIA FRUC ready: {Width}x{Height} {Format}, {Textures} textures registered, synchronised on a shared D3D11 fence")]
    public static partial void FrucReady(this ILogger log, int width, int height, FrucSurfaceFormat format, int textures);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NVIDIA FRUC unavailable: {Reason}")]
    public static partial void FrucUnavailable(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NVIDIA FRUC stopped generating: {Reason}")]
    public static partial void FrucFailed(this ILogger log, string reason);
}
