// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>The streaming service's token for the console offering, with its regions (at least one).</summary>
public sealed record StreamingToken(string Value, DateTimeOffset ExpiresAt, IReadOnlyList<StreamingRegion> Regions)
{
    public StreamingRegion DefaultRegion
    {
        get
        {
            foreach (var region in Regions)
            {
                if (region.IsDefault)
                {
                    return region;
                }
            }

            return Regions[0];
        }
    }

    public bool IsExpired(TimeProvider clock)
    {
        return clock.GetUtcNow() >= ExpiresAt - XboxEndpoints.ExpiryMargin;
    }
}
