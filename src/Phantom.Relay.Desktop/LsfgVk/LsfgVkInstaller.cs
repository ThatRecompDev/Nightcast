// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// Makes the pinned lsfg-vk ready in Nightcast's own folder (<see cref="LsfgVkFolders"/>): when its layer is not
/// unpacked yet, the official archive is downloaded (<see cref="IArchiveSource"/>), refused unless its SHA-256 is the
/// pinned one (<see cref="LsfgVkRelease.Sha256"/>, given as <c>sha256</c>; <see cref="LsfgVkRelease.Matches"/>), decompressed (<see cref="IArchiveDecompressor"/>) and only the
/// layer and its manifest unpacked (<see cref="LsfgVkArchive"/>). The download and the tar are removed afterwards,
/// whatever happened. Nothing is written outside the folder.
/// </summary>
public sealed class LsfgVkInstaller
{
    private readonly LsfgVkFolders _folders;
    private readonly IArchiveSource _source;
    private readonly IArchiveDecompressor _decompressor;
    private readonly string _sha256;

    public LsfgVkInstaller(LsfgVkFolders folders, IArchiveSource source, IArchiveDecompressor decompressor, string sha256)
    {
        _folders = folders;
        _source = source;
        _decompressor = decompressor;
        _sha256 = sha256;
    }

    /// <summary>Null once the layer is unpacked (at once when it already was); else why it could not be.</summary>
    public async Task<string?> EnsureAsync(CancellationToken cancellation)
    {
        if (_folders.Installed)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(_folders.Base);
            await _source.DownloadAsync(LsfgVkRelease.Archive, _folders.DownloadPath, cancellation);

            return Unpack();
        }
        catch (Exception failed) when (failed is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return $"the download of lsfg-vk {LsfgVkRelease.Version} failed ({failed.Message})";
        }
        finally
        {
            File.Delete(_folders.DownloadPath);
            File.Delete(_folders.TarPath);
        }
    }

    /// <summary>The downloaded archive checked, decompressed and its two files unpacked; null when done, else why not.</summary>
    private string? Unpack()
    {
        bool pinned;

        using (var archive = File.OpenRead(_folders.DownloadPath))
        {
            pinned = LsfgVkRelease.Matches(archive, _sha256);
        }

        if (!pinned)
        {
            return $"the download is not the official lsfg-vk {LsfgVkRelease.Version} (its SHA-256 differs), so it was refused";
        }

        if (!_decompressor.TryDecompress(_folders.DownloadPath, _folders.TarPath, out var problem))
        {
            return problem;
        }

        IReadOnlyList<string> missing;

        using (var tar = File.OpenRead(_folders.TarPath))
        {
            missing = LsfgVkArchive.Unpack(tar, LsfgVkRelease.Members, _folders.PartialRoot, _folders.Root);
        }

        if (missing.Count > 0)
        {
            return $"the lsfg-vk archive lacks {string.Join(", ", missing)}";
        }

        return null;
    }
}
