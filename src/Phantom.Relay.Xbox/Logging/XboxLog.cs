// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Xbox.Http;

namespace Phantom.Relay.Xbox.Logging;

/// <summary>The Xbox layer's log messages, source-generated so a disabled level costs nothing.</summary>
public static partial class XboxLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "The user store refused the account tokens; sign-in lasts until the app closes")]
    public static partial void CredentialsNotKept(this ILogger log);

    [LoggerMessage(Level = LogLevel.Information, Message = "Renewing the Microsoft account token")]
    public static partial void RenewingAccountToken(this ILogger log);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The {What} request failed with HTTP {Status}")]
    public static partial void SignInRequestFailed(this ILogger log, string what, int status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session started at {Path}")]
    public static partial void SessionStarted(this ILogger log, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Verb} {Path} failed with HTTP {Status}")]
    public static partial void SessionRequestFailed(this ILogger log, HttpVerb verb, string path, int status);
}
