// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.App.Logging;

/// <summary>The app's log messages, source-generated so a disabled level costs nothing. None carries a token.</summary>
public static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Stats: {Stats}")]
    public static partial void Stats(this ILogger log, string stats);
}
