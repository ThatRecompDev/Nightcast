// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Updates;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>Downloads over HTTPS, anonymously, as Nightcast's update check does (its user agent and download time limit).</summary>
public sealed class HttpArchiveSource : IArchiveSource
{
    public async Task DownloadAsync(Uri address, string path, CancellationToken cancellation)
    {
        using var client = new HttpClient { Timeout = UpdateSource.DownloadTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UpdateSource.UserAgent);

        await using var source = await client.GetStreamAsync(address, cancellation);
        await using var file = File.Create(path);
        await source.CopyToAsync(file, cancellation);
    }
}
