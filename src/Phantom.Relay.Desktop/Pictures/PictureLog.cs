// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>How the decoded pictures reach the screen, logged once per change; source-generated.</summary>
internal static partial class PictureLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Pictures stay on the GPU: a ring of {Slots} shared NV12 textures of {Width}x{Height}")]
    public static partial void Sharing(this ILogger log, int slots, int width, int height);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pictures are read back to the CPU: {Reason}")]
    public static partial void ReadBack(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Frame generation: LSFG x{Multiplier} ({Mode}), flow scale {FlowScale}")]
    public static partial void GenerationOn(this ILogger log, int multiplier, string mode, float flowScale);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Frame generation off, the stream plays without it: {Reason}")]
    public static partial void GenerationOff(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "The decoder moved to the renderer's GPU (adapter 0x{Luid:X})")]
    public static partial void MovedToAdapter(this ILogger log, long luid);
}
