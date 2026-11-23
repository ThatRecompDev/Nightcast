// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>Turns lsfg-vk's xz-compressed archive into a plain tar (.NET reads tar but not xz).</summary>
public interface IArchiveDecompressor
{
    /// <summary>Writes <paramref name="archive"/> decompressed to <paramref name="tar"/>; false, with the reason, when it could not.</summary>
    bool TryDecompress(string archive, string tar, out string? problem);
}
