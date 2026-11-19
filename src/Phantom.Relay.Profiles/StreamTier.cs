// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Profiles;

/// <summary>The stream tier a profile asks the console for.</summary>
public sealed record StreamTier(StreamResolution Resolution)
{
    /// <summary>The highest tier: quality comes first, and the service falls back to the best it grants.</summary>
    public static StreamTier Standard { get; } = new(StreamResolution.P1440);
}
