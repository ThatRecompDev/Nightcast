// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session.Payloads;

namespace Phantom.Relay.Xbox.Session;

/// <summary>Builds the play request the web player sends for a console.</summary>
public static class PlayRequests
{
    public static PlayRequest For(string serverId, StreamSettings settings)
    {
        var clientSettings = new PlaySettings(
            XHomeProtocol.NanoVersion,
            EnableTextToSpeech: false,
            HighContrast: 0,
            settings.Locale,
            UseIceConnection: false,
            settings.TimezoneOffsetMinutes,
            XHomeProtocol.SdkType,
            settings.OsName);

        return new PlayRequest(string.Empty, string.Empty, string.Empty, serverId, [], clientSettings);
    }
}
