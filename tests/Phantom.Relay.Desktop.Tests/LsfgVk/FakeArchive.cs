// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Formats.Tar;
using System.Text;
using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

/// <summary>Small tars shaped like lsfg-vk's archive (GNU format, names led by <c>./</c>), with text for each file's bytes.</summary>
internal static class FakeArchive
{
    public const string LayerText = "not really a layer";
    public const string ManifestText = "{ \"layer\": {} }";

    /// <summary>The release's tree: folders, the two members, the files Nightcast skips, and a name that leaves the tree.</summary>
    public static byte[] Release()
    {
        return Tar(
            ("./lib/", null),
            ("./" + LsfgVkRelease.LayerLibrary, LayerText),
            ("./lib/liblsfg-vk-layer.x86.so", "32-bit"),
            ("./" + LsfgVkRelease.LayerManifest, ManifestText),
            ("./bin/lsfg-vk-ui", "ui"),
            ("../outside.so", "escaped"));
    }

    /// <summary>A tar of <paramref name="entries"/>: a folder where the text is null, else a file with that text.</summary>
    public static byte[] Tar(params (string Name, string? Text)[] entries)
    {
        using var buffer = new MemoryStream();

        using (var writer = new TarWriter(buffer, TarEntryFormat.Gnu, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                writer.WriteEntry(Entry(name, text));
            }
        }

        return buffer.ToArray();
    }

    private static GnuTarEntry Entry(string name, string? text)
    {
        if (text is null)
        {
            return new GnuTarEntry(TarEntryType.Directory, name);
        }

        return new GnuTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(text)) };
    }
}
