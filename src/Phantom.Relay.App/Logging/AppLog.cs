// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.App.Logging;

/// <summary>The app's log messages, source-generated so a disabled level costs nothing. None carries a token.</summary>
public static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Stats: {Stats}")]
    public static partial void Stats(this ILogger log, string stats);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stats on screen: {Face} at {Pixels} px")]
    public static partial void OverlayFont(this ILogger log, string face, float pixels);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stats on screen: the system gave no font to draw them with; F3 shows them in the title only")]
    public static partial void OverlayFontMissing(this ILogger log);

    [LoggerMessage(Level = LogLevel.Information, Message = "Frame generation chosen live: {Setting}")]
    public static partial void GenerationChosen(this ILogger log, string setting);
}
