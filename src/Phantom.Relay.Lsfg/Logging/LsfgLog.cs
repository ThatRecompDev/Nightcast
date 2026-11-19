// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Lsfg.Upscaling;

namespace Phantom.Relay.Lsfg.Logging;

/// <summary>
/// The frame generation's log messages, source-generated so a disabled level costs nothing. They carry resource ids,
/// kinds and counts only: never a shader's bytes, never a path.
/// </summary>
public static partial class LsfgLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Lossless.dll: {Resources} raw-data resources, {Shaders} compute shaders reflected")]
    public static partial void CatalogLoaded(this ILogger log, int resources, int shaders);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Shader {Id}: compute {GroupWidth}x{GroupHeight}, {Textures} textures, {Outputs} outputs, {Samplers} samplers, {ConstantBuffers} constant buffers ({ConstantBytes} bytes)")]
    public static partial void ShaderReflected(this ILogger log, int id, int groupWidth, int groupHeight, int textures, int outputs, int samplers, int constantBuffers, int constantBytes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resource {Id} skipped: {Reason}")]
    public static partial void ResourceSkipped(this ILogger log, int id, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Frame generation ready: {Width}x{Height}, flow {FlowWidth}x{FlowHeight}, {Mode} shaders, {Passes} passes per real frame, {GeneratedPasses} per generated frame")]
    public static partial void PipelineReady(this ILogger log, int width, int height, int flowWidth, int flowHeight, string mode, int passes, int generatedPasses);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Frame generation unavailable: {Reason}")]
    public static partial void Unavailable(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lossless upscaler ready: {Kind} (network {Level}), {InputWidth}x{InputHeight} to {OutputWidth}x{OutputHeight}, {Passes} passes")]
    public static partial void UpscalerReady(this ILogger log, LosslessUpscalerKind kind, int level, int inputWidth, int inputHeight, int outputWidth, int outputHeight, int passes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lossless upscaler unavailable: {Reason}")]
    public static partial void UpscalerUnavailable(this ILogger log, string reason);
}
