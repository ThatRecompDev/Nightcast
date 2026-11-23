// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Fonts;

/// <summary>
/// The families a face asked for is drawn with on Linux, in order. A monospaced face (Consolas, Courier New, Lucida
/// Console, Cascadia, or fontconfig's generic <c>monospace</c>) takes the monospaced families Arch Linux (SteamOS's base)
/// and the other distributions install: DejaVu Sans Mono, Noto Sans Mono, Liberation Mono (metric-compatible with
/// Courier New), then Hack, Ubuntu Mono and Droid Sans Mono. Any other face takes the sans-serif families. A family
/// whose name the face is (spaces ignored) comes first. These file names are the families' own, as their packages
/// install them.
/// </summary>
internal static class LinuxFontFamilies
{
    public static readonly LinuxFontFamily[] Monospace =
    [
        new("DejaVu Sans Mono", "DejaVuSansMono.ttf", "DejaVuSansMono-Bold.ttf", "DejaVuSansMono-Oblique.ttf", "DejaVuSansMono-BoldOblique.ttf"),
        new("Noto Sans Mono", "NotoSansMono-Regular.ttf", "NotoSansMono-Bold.ttf", null, null),
        new("Liberation Mono", "LiberationMono-Regular.ttf", "LiberationMono-Bold.ttf", "LiberationMono-Italic.ttf", "LiberationMono-BoldItalic.ttf"),
        new("Hack", "Hack-Regular.ttf", "Hack-Bold.ttf", "Hack-Italic.ttf", "Hack-BoldItalic.ttf"),
        new("Ubuntu Mono", "UbuntuMono-R.ttf", "UbuntuMono-B.ttf", "UbuntuMono-RI.ttf", "UbuntuMono-BI.ttf"),
        new("Droid Sans Mono", "DroidSansMono.ttf", null, null, null),
    ];

    public static readonly LinuxFontFamily[] Sans =
    [
        new("DejaVu Sans", "DejaVuSans.ttf", "DejaVuSans-Bold.ttf", "DejaVuSans-Oblique.ttf", "DejaVuSans-BoldOblique.ttf"),
        new("Noto Sans", "NotoSans-Regular.ttf", "NotoSans-Bold.ttf", "NotoSans-Italic.ttf", "NotoSans-BoldItalic.ttf"),
        new("Liberation Sans", "LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf", "LiberationSans-Italic.ttf", "LiberationSans-BoldItalic.ttf"),
    ];

    /// <summary>The Windows and generic faces that ask for a monospaced font.</summary>
    private static readonly string[] MonospaceFaces =
    [
        "Consolas", "Courier New", "Courier", "Lucida Console", "Cascadia Mono", "Cascadia Code", "monospace", "Menlo", "Monaco",
    ];

    /// <summary>The families to try for <paramref name="face"/>, in order.</summary>
    public static IEnumerable<LinuxFontFamily> For(string face)
    {
        var all = Monospace.Concat(Sans);
        var named = all.Where(family => SameName(family.Face, face));
        var monospaced = Monospace.Any(family => SameName(family.Face, face)) || MonospaceFaces.Any(alias => SameName(alias, face));

        if (monospaced)
        {
            return named.Concat(Monospace).Distinct();
        }

        return named.Concat(Sans).Distinct();
    }

    /// <summary>True for a face named the same as <paramref name="name"/>, case and spaces ignored.</summary>
    private static bool SameName(string name, string face)
    {
        return string.Equals(name.Replace(" ", string.Empty, StringComparison.Ordinal), face.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }
}
