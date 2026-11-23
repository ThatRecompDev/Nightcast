// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Ui.Text;

namespace Phantom.Relay.Desktop.Fonts;

/// <summary>
/// The system's fonts on Linux, found by their files: the TrueType files under the font folders fontconfig reads (the
/// user's own first, then the machine's), indexed by file name once, and a face resolved to the first family of
/// <see cref="LinuxFontFamilies.For"/> whose file is there: Consolas (the stats overlay's face) to DejaVu Sans Mono,
/// Noto Sans Mono or Liberation Mono, whichever is installed. fontconfig itself is not asked: its own aliases send
/// Consolas to the default sans-serif face, which is not monospaced, and a lookup through it is an engine port
/// (an engine candidate, the Linux twin of the engine's WindowsSystemFonts). Points are at the standard 96 pixels per
/// inch: gamescope and the X server report no other.
/// </summary>
public sealed class LinuxSystemFonts : ISystemFonts
{
    private const string Pattern = "*.ttf";
    private const string MachineFonts = "/usr/share/fonts";
    private const string LocalFonts = "/usr/local/share/fonts";
    private const string UserFonts = ".local/share/fonts";
    private const string OldUserFonts = ".fonts";
    private const string DataFonts = "fonts";

    private static readonly EnumerationOptions Everywhere = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    private readonly IReadOnlyList<string> _folders;
    private Dictionary<string, string>? _files;

    /// <summary>The fonts under <paramref name="folders"/>, the first folder's file winning a name found twice.</summary>
    public LinuxSystemFonts(IReadOnlyList<string> folders)
    {
        _folders = folders;
    }

    public int PixelsPerInch => NoSystemFonts.StandardPixelsPerInch;

    /// <summary>
    /// The folders fontconfig reads, in its order: the user's data folder's fonts (<c>$XDG_DATA_HOME/fonts</c>, else
    /// <c>~/.local/share/fonts</c>), the old <c>~/.fonts</c>, then <c>/usr/local/share/fonts</c> and <c>/usr/share/fonts</c>.
    /// </summary>
    public static IReadOnlyList<string> Folders(string? home, string? dataHome)
    {
        var folders = new List<string>();

        if (!string.IsNullOrEmpty(dataHome))
        {
            folders.Add(Path.Combine(dataHome, DataFonts));
        }
        else if (!string.IsNullOrEmpty(home))
        {
            folders.Add(Path.Combine(home, UserFonts));
        }

        if (!string.IsNullOrEmpty(home))
        {
            folders.Add(Path.Combine(home, OldUserFonts));
        }

        folders.Add(LocalFonts);
        folders.Add(MachineFonts);

        return folders;
    }

    public SystemFont? Find(string face, bool bold, bool italic)
    {
        ArgumentNullException.ThrowIfNull(face);

        var files = Files();

        foreach (var family in LinuxFontFamilies.For(face))
        {
            foreach (var cut in family.Cuts(bold, italic))
            {
                if (files.TryGetValue(cut.File, out var path))
                {
                    return new SystemFont(family.Face, cut.File, File.ReadAllBytes(path), cut.Bold, cut.Italic);
                }
            }
        }

        return null;
    }

    /// <summary>Every TrueType file under the folders by its name, read once.</summary>
    private Dictionary<string, string> Files()
    {
        if (_files is not null)
        {
            return _files;
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var folder in _folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(folder, Pattern, Everywhere))
            {
                files.TryAdd(Path.GetFileName(path), path);
            }
        }

        _files = files;

        return files;
    }
}
