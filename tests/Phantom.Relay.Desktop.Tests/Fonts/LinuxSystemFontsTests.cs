// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Fonts;

namespace Phantom.Relay.Desktop.Tests.Fonts;

/// <summary>The fonts are found by file name in folders the test makes; the files' bytes are stand-ins, never parsed.</summary>
public sealed class LinuxSystemFontsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nightcast-fonts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Consolas_is_drawn_with_an_installed_monospaced_family()
    {
        var machine = Folder("usr", "share", "fonts");
        Put(machine, "TTF", "DejaVuSans.ttf");
        Put(machine, "liberation", "LiberationMono-Regular.ttf");
        var fonts = new LinuxSystemFonts([machine]);

        var font = fonts.Find("Consolas", bold: false, italic: false);

        Assert.NotNull(font);
        Assert.Equal("Liberation Mono", font.Face);
        Assert.Equal("LiberationMono-Regular.ttf", font.File);
        Assert.Equal(new byte[] { (byte)'L' }, font.Data);
    }

    [Fact]
    public void Dejavu_sans_mono_comes_before_noto_and_liberation()
    {
        var machine = Folder("fonts");
        Put(machine, "noto", "NotoSansMono-Regular.ttf");
        Put(machine, "liberation", "LiberationMono-Regular.ttf");
        Put(machine, "TTF", "DejaVuSansMono.ttf");

        var font = new LinuxSystemFonts([machine]).Find("Consolas", false, false);

        Assert.Equal("DejaVu Sans Mono", font!.Face);
    }

    [Fact]
    public void A_face_named_by_its_own_family_is_taken_first()
    {
        var machine = Folder("fonts");
        Put(machine, "TTF", "DejaVuSansMono.ttf");
        Put(machine, "noto", "NotoSansMono-Regular.ttf");

        var font = new LinuxSystemFonts([machine]).Find("Noto Sans Mono", false, false);

        Assert.Equal("Noto Sans Mono", font!.Face);
    }

    [Fact]
    public void Another_face_takes_a_sans_serif_family()
    {
        var machine = Folder("fonts");
        Put(machine, "TTF", "DejaVuSansMono.ttf");
        Put(machine, "noto", "NotoSans-Regular.ttf");

        var font = new LinuxSystemFonts([machine]).Find("Segoe UI", false, false);

        Assert.Equal("Noto Sans", font!.Face);
    }

    [Fact]
    public void A_bold_cut_is_taken_where_the_family_has_it_else_the_regular_one()
    {
        var machine = Folder("fonts");
        Put(machine, "TTF", "DejaVuSansMono.ttf");
        Put(machine, "TTF", "DejaVuSansMono-Bold.ttf");
        var fonts = new LinuxSystemFonts([machine]);

        var bold = fonts.Find("Consolas", bold: true, italic: false);
        var italic = fonts.Find("Consolas", bold: false, italic: true);

        Assert.Equal(("DejaVuSansMono-Bold.ttf", true, false), (bold!.File, bold.Bold, bold.Italic));
        Assert.Equal(("DejaVuSansMono.ttf", false, false), (italic!.File, italic.Bold, italic.Italic));
    }

    [Fact]
    public void The_users_own_folder_wins_a_name_found_twice()
    {
        var user = Folder("home", ".local", "share", "fonts");
        var machine = Folder("usr", "share", "fonts");
        Put(machine, "TTF", "DejaVuSansMono.ttf");
        Directory.CreateDirectory(user);
        File.WriteAllBytes(Path.Combine(user, "DejaVuSansMono.ttf"), [(byte)'U']);

        var font = new LinuxSystemFonts([user, Folder("missing"), machine]).Find("Consolas", false, false);

        Assert.Equal(new byte[] { (byte)'U' }, font!.Data);
    }

    [Fact]
    public void Without_any_known_family_no_face_resolves()
    {
        var machine = Folder("fonts");
        Put(machine, "misc", "SomethingElse.ttf");
        Put(machine, "misc", "DejaVuSansMono.otf");

        Assert.Null(new LinuxSystemFonts([machine]).Find("Consolas", false, false));
        Assert.Null(new LinuxSystemFonts([]).Find("Consolas", false, false));
    }

    [Fact]
    public void The_folders_are_fontconfigs_the_users_first()
    {
        Assert.Equal(
            [Path.Combine("/home/deck", ".local/share/fonts"), Path.Combine("/home/deck", ".fonts"), "/usr/local/share/fonts", "/usr/share/fonts"],
            LinuxSystemFonts.Folders("/home/deck", null));
        Assert.Equal(
            [Path.Combine("/data", "fonts"), Path.Combine("/home/deck", ".fonts"), "/usr/local/share/fonts", "/usr/share/fonts"],
            LinuxSystemFonts.Folders("/home/deck", "/data"));
        Assert.Equal(["/usr/local/share/fonts", "/usr/share/fonts"], LinuxSystemFonts.Folders(null, null));
    }

    private string Folder(params string[] parts)
    {
        return Path.Combine([_root, .. parts]);
    }

    /// <summary>A stand-in font file whose one byte is its name's first letter.</summary>
    private static void Put(string folder, string subfolder, string name)
    {
        var directory = Path.Combine(folder, subfolder);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), [(byte)name[0]]);
    }
}
