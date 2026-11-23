// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>The recorder's log messages, source-generated; none names an account, a console or a full path.</summary>
internal static partial class RecorderLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording to {Folder}: waiting for a key frame")]
    public static partial void RecordingStarted(this ILogger log, string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording not started: {Reason}")]
    public static partial void RecordingRefused(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording stopped; finishing {Folder}")]
    public static partial void RecordingStopped(this ILogger log, string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording saved to {Folder}")]
    public static partial void RecordingSaved(this ILogger log, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recording failed: {Reason}")]
    public static partial void RecordingFailed(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "source.mp4 not written: {Reason}")]
    public static partial void SourceRefused(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "source.mp4: {Units} units, {Packets} sound packets ({Concealed} lost ones concealed), {Seconds:F1} s; dropped {DroppedUnits} units and {DroppedPackets} packets (queues full)")]
    public static partial void PassthroughWritten(this ILogger log, long units, long packets, long concealed, double seconds, long droppedUnits, long droppedPackets);

    [LoggerMessage(Level = LogLevel.Information, Message = "shown.mp4: {Encoder} ({Hardware}), {Codec} {Width}x{Height} at {Rate} fps")]
    public static partial void ShownEncoder(this ILogger log, string encoder, string hardware, string codec, int width, int height, int rate);

    [LoggerMessage(Level = LogLevel.Warning, Message = "shown.mp4 not written: {Reason}")]
    public static partial void ShownSkipped(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Comparison skipped: {Reason}")]
    public static partial void CompareSkipped(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Comparison made: {File} in {Seconds:F1} s")]
    public static partial void Compared(this ILogger log, string file, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Comparison failed: {Reason}")]
    public static partial void CompareFailed(this ILogger log, string reason);
}
