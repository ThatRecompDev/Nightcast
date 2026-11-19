// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>
/// The x-ms-device-info header: the service decides from it, with the play request's OS name, which tiers this client
/// may receive. Its shape is the official web player's (play.xbox.com in a desktop browser, seen 2026-11-19: the
/// hardware block's <c>platformType</c> and <c>sdkType</c> spelled so), with a 1440p display, so the display it reports
/// is never what limits the stream's tier.
/// </summary>
public static class DeviceInfo
{
    public const string Json =
        """{"appInfo":{"env":{"clientAppId":"play.xbox.com","clientAppType":"browser","clientSdkVersion":"placeholder","httpEnvironment":"prod","sdkInstallId":""}},"dev":{"displayInfo":{"dimensions":{"heightInPixels":1440,"widthInPixels":2560},"pixelDensity":{"dpiX":1,"dpiY":1}},"browser":{"browserName":"edge","browserVersion":"157.0.4341.60"},"hw":{"make":"unknown","model":"unknown","platformType":"desktop","sdkType":"web"},"os":{"name":"windows","ver":"19.0.0","platform":"desktop"}}}""";
}
