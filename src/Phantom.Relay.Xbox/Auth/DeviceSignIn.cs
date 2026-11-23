// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>
/// A sign-in by code under way: the player opens <see cref="Page"/> on any device and enters <see cref="UserCode"/>
/// before <see cref="ExpiresAt"/>; the app asks every <see cref="Interval"/> with the device code, which never leaves it.
/// </summary>
public sealed record DeviceSignIn(string UserCode, Uri Page, TimeSpan Interval, DateTimeOffset ExpiresAt)
{
    /// <summary>The code the app asks with; kept out of the record's text so it never reaches a log.</summary>
    public string DeviceCode { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"{UserCode} at {Page}";
    }
}
