// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

/// <summary>An archive source that "downloads" fixed bytes and counts its downloads.</summary>
internal sealed class BytesSource : IArchiveSource
{
    private readonly byte[] _bytes;

    public BytesSource(byte[] bytes)
    {
        _bytes = bytes;
    }

    public int Downloads { get; private set; }

    public Uri? Asked { get; private set; }

    public Task DownloadAsync(Uri address, string path, CancellationToken cancellation)
    {
        Downloads++;
        Asked = address;
        File.WriteAllBytes(path, _bytes);

        return Task.CompletedTask;
    }
}
