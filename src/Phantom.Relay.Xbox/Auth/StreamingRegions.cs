// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Auth.Payloads;

namespace Phantom.Relay.Xbox.Auth;

/// <summary>Reads the regions of a streaming login, skipping any without a name or a valid absolute address.</summary>
public static class StreamingRegions
{
    public static IReadOnlyList<StreamingRegion> From(IReadOnlyList<RegionReply>? replies)
    {
        var regions = new List<StreamingRegion>();

        if (replies is null)
        {
            return regions;
        }

        foreach (var reply in replies)
        {
            if (reply.Name is null || !Uri.TryCreate(reply.BaseUri, UriKind.Absolute, out var baseUri))
            {
                continue;
            }

            regions.Add(new StreamingRegion(reply.Name, baseUri, reply.IsDefault));
        }

        return regions;
    }
}
