// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Profiles;

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Frame generation: NVIDIA FRUC x{Multiplier}")]
    public static partial void FrucOn(this ILogger log, int multiplier);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NVIDIA FRUC off, LSFG runs instead: {Reason}")]
    public static partial void FrucFallback(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Frame generation x{Requested} capped to x{Allowed}: {Reason}")]
    public static partial void GenerationCapped(this ILogger log, int requested, int allowed, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Frame generation off, the stream plays without it: {Reason}")]
    public static partial void GenerationOff(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Upscaling on the decoder's GPU: {Method}, {Width}x{Height} to {OutputWidth}x{OutputHeight}")]
    public static partial void UpscaleOn(this ILogger log, UpscaleMethod method, int width, int height, int outputWidth, int outputHeight);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} off, the renderer upscales with FSR 1 instead: {Reason}")]
    public static partial void UpscaleOff(this ILogger log, UpscaleMethod method, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "The decoder moved to the renderer's GPU (adapter 0x{Luid:X})")]
    public static partial void MovedToAdapter(this ILogger log, long luid);
}
