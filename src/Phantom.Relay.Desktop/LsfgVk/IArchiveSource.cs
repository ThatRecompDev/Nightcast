// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>Where lsfg-vk's archive is fetched from: the official build server, or a test's bytes.</summary>
public interface IArchiveSource
{
    /// <summary>Writes the archive at <paramref name="address"/> to <paramref name="path"/>.</summary>
    Task DownloadAsync(Uri address, string path, CancellationToken cancellation);
}
