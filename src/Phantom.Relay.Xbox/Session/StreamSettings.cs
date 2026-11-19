// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>What a session is started with: the tier, the console's language and the player's time zone.</summary>
public sealed record StreamSettings(StreamResolution Resolution, string Locale, int TimezoneOffsetMinutes)
{
    /// <summary>The operating system reported in the play request, which chooses the tier.</summary>
    public string OsName
    {
        get
        {
            switch (Resolution)
            {
                case StreamResolution.P720:
                    return "android";
                case StreamResolution.P1080High:
                case StreamResolution.P1440:
                    // The highest tier known by OS name; 1440p's own request is not confirmed yet.
                    return "tizen";
                default:
                    return "windows";
            }
        }
    }
}
