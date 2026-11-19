// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Setup;

/// <summary>What a session is started with: the tier asked for, the console's language and the player's time zone now.</summary>
public static class SessionSettings
{
    /// <summary>The console's language: English until the app has a setting for it.</summary>
    public const string Locale = "en-US";

    public static StreamSettings For(StreamResolution resolution, TimeProvider clock)
    {
        int offset = (int)clock.LocalTimeZone.GetUtcOffset(clock.GetUtcNow()).TotalMinutes;

        return new StreamSettings(resolution, Locale, offset);
    }
}
