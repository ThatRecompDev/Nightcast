// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.Stream.Logging;

/// <summary>The stream's log messages, source-generated so a disabled level costs nothing. None carries a token.</summary>
public static partial class StreamLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Stream: {State}")]
    public static partial void StateChanged(this ILogger log, StreamState state);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream failed: {Reason}")]
    public static partial void Failed(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "The console runs title {TitleId}; profile {Profile}")]
    public static partial void TitleChanged(this ILogger log, string titleId, string profile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A session keep-alive was refused: {Reason}")]
    public static partial void KeepAliveRefused(this ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stopping the session failed: {Reason}")]
    public static partial void StopRefused(this ILogger log, string reason);
}
