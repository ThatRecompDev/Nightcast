// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>
/// The x-ms-device-info header: the service decides from it, with the play request's OS name, which tiers this client
/// may receive, so the client presents as the web player on a desktop browser with a 1440p display: the display it
/// reports must never be what limits the stream's tier.
/// </summary>
public static class DeviceInfo
{
    public const string Json =
        """{"appInfo":{"env":{"clientAppId":"www.xbox.com","clientAppType":"browser","clientAppVersion":"26.1.97","clientSdkVersion":"10.3.7","httpEnvironment":"prod","sdkInstallId":""}},"dev":{"hw":{"make":"Microsoft","model":"unknown","sdktype":"web"},"os":{"name":"windows","ver":"22631.2715","platform":"desktop"},"displayInfo":{"dimensions":{"widthInPixels":2560,"heightInPixels":1440},"pixelDensity":{"dpiX":1,"dpiY":1}},"browser":{"browserName":"chrome","browserVersion":"130.0"}}}""";
}
