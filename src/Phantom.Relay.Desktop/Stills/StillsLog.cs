// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.Desktop.Stills;

/// <summary>The screenshots' log messages, source-generated; none names a full path.</summary>
internal static partial class StillsLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Screenshot saved: {File}")]
    public static partial void ScreenshotSaved(this ILogger log, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Screenshot not saved: {Reason}")]
    public static partial void ScreenshotFailed(this ILogger log, string reason);
}
