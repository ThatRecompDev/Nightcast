// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

/// <summary>A "decompressor" for archives that are plain tars already: it copies the file.</summary>
internal sealed class CopyDecompressor : IArchiveDecompressor
{
    public bool TryDecompress(string archive, string tar, out string? problem)
    {
        File.Copy(archive, tar, overwrite: true);
        problem = null;

        return true;
    }
}
