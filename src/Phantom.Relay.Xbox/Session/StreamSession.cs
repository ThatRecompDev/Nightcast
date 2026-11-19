// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>A started session: its id, its path (no leading slash) and the region's base address.</summary>
public sealed record StreamSession(string SessionId, string SessionPath, Uri BaseUri)
{
    public Uri Address(string suffix)
    {
        return new Uri(BaseUri, $"{SessionPath}/{suffix}");
    }

    public Uri Root => new(BaseUri, SessionPath);
}
