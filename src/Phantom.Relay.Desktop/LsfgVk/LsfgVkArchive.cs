// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Formats.Tar;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// Unpacks the members Nightcast needs (<see cref="LsfgVkRelease.Members"/>) from lsfg-vk's archive, once it is a
/// plain tar (<see cref="IArchiveDecompressor"/> undid its xz): each named member that is a regular file is written below
/// the destination at its own relative path; every other entry is skipped unread, so no name in the archive can write
/// anywhere else. The members go into a partial folder first, which becomes the destination only when all of them came.
/// </summary>
public static class LsfgVkArchive
{
    private const string CurrentFolder = "./";

    /// <summary>
    /// The <paramref name="members"/> of the tar in <paramref name="tar"/> into <paramref name="destination"/> (through
    /// <paramref name="partial"/>, replaced whole); the members it lacked, none when it is done.
    /// </summary>
    public static IReadOnlyList<string> Unpack(System.IO.Stream tar, IReadOnlyList<string> members, string partial, string destination)
    {
        ArgumentNullException.ThrowIfNull(members);
        Remove(partial);
        Directory.CreateDirectory(partial);

        var missing = new HashSet<string>(members, StringComparer.Ordinal);

        using (var reader = new TarReader(tar, leaveOpen: true))
        {
            var entry = reader.GetNextEntry();

            while (entry is not null)
            {
                Take(entry, partial, missing);
                entry = reader.GetNextEntry();
            }
        }

        if (missing.Count > 0)
        {
            Remove(partial);

            return [.. missing.Order(StringComparer.Ordinal)];
        }

        Remove(destination);
        Directory.Move(partial, destination);

        return [];
    }

    /// <summary>An entry still <paramref name="missing"/> and a regular file is written below <paramref name="partial"/>; any other is skipped.</summary>
    private static void Take(TarEntry entry, string partial, HashSet<string> missing)
    {
        var regular = entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile;

        if (!regular || !missing.Remove(NameOf(entry.Name)))
        {
            return;
        }

        var target = Path.Combine(partial, NameOf(entry.Name));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        entry.ExtractToFile(target, overwrite: true);
    }

    /// <summary>A member's name as <see cref="LsfgVkRelease.Members"/> writes it: without the archive's leading <c>./</c>.</summary>
    public static string NameOf(string entryName)
    {
        ArgumentNullException.ThrowIfNull(entryName);

        if (entryName.StartsWith(CurrentFolder, StringComparison.Ordinal))
        {
            return entryName[CurrentFolder.Length..];
        }

        return entryName;
    }

    private static void Remove(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
