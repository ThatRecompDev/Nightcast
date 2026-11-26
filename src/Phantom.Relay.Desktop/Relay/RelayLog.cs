// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.Desktop.Relay;

/// <summary>The TURN relay's setup messages, source-generated; none names the key, a credential or a full path.</summary>
internal static partial class RelayLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "TURN relay ready ({Server}), for when no direct path reaches the console")]
    public static partial void RelayReady(this ILogger log, string server);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TURN relay off: {Reason}")]
    public static partial void RelayOff(this ILogger log, string reason);
}
