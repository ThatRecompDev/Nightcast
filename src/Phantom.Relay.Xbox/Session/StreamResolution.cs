// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>
/// The stream tier. The service picks it from the operating system the client reports (<see cref="StreamSettings.OsName"/>),
/// not from a resolution field.
/// </summary>
public enum StreamResolution
{
    P720,
    P1080,

    /// <summary>1080p with the higher bitrate ladder the service gives television clients.</summary>
    P1080High,

    /// <summary>
    /// 1440p at the higher bitrates remote play gained in 2026; the console must output 1440p or 4K. How a client asks
    /// for it is not confirmed yet (docs/architecture/xhome.md); the service falls back to the best tier it grants.
    /// </summary>
    P1440,
}
