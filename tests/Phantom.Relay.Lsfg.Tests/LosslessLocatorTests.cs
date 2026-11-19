// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Tests;

public sealed class LosslessLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lsfg-locator-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Steam_library_folders_are_read_from_libraryfolders_vdf()
    {
        const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"E:\\\\Games\\\\Steam\"\n\t\t\"label\"\t\t\"\"\n\t}\n}\n";

        Assert.Equal([@"C:\Steam", @"E:\Games\Steam"], LosslessLocator.ParseLibraryFolders(vdf));
    }

    [Fact]
    public void The_configured_path_wins_when_the_file_is_there()
    {
        var configured = Touch(Path.Combine(_root, "custom", "Lossless.dll"));
        var library = Path.Combine(_root, "library");
        Touch(Path.Combine(library, LosslessLocator.RelativePath));

        Assert.Equal(configured, LosslessLocator.Find(configured, [library]));
    }

    [Fact]
    public void Without_a_configured_path_the_first_steam_library_holding_lossless_scaling_is_used()
    {
        var empty = Path.Combine(_root, "empty");
        var library = Path.Combine(_root, "library");
        var expected = Touch(Path.Combine(library, LosslessLocator.RelativePath));

        Assert.Equal(expected, LosslessLocator.Find(null, [empty, library]));
        Assert.Equal(expected, LosslessLocator.Find(Path.Combine(_root, "missing.dll"), [empty, library]));
    }

    [Fact]
    public void Nothing_is_found_when_no_library_holds_it()
    {
        Assert.Null(LosslessLocator.Find(null, [Path.Combine(_root, "empty")]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);

        return path;
    }
}
