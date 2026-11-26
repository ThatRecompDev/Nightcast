// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Stream.Media;

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pictures: {New:F0} new and {Repeated:F0} repeated a second, cadence {Cadence}")]
    public static partial void PictureCadence(this ILogger log, double @new, double repeated, PictureCadence cadence);

    [LoggerMessage(Level = LogLevel.Information, Message = "Latest access units, bytes: {Sizes}")]
    public static partial void UnitSizes(this ILogger log, string sizes);

    [LoggerMessage(Level = LogLevel.Information, Message = "ICE connected to the console's {Kind} candidate {Path} (of {Candidates}), round trip {RoundTripMs:F1} ms")]
    public static partial void IceConnected(this ILogger log, string kind, string path, int candidates, double roundTripMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "TURN relay allocated in {Milliseconds:F0} ms")]
    public static partial void RelayAllocated(this ILogger log, double milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TURN relay unavailable ({State}, error {Code}); direct paths only")]
    public static partial void RelayUnavailable(this ILogger log, string state, int code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bitrate feedback offered: {Offered}; the answer kept: {Kept}")]
    public static partial void BitrateFeedback(this ILogger log, string offered, string kept);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stopping the session failed: {Reason}")]
    public static partial void StopRefused(this ILogger log, string reason);
}
