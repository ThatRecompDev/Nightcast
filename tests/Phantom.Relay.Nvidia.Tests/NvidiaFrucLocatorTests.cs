// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Nvidia.Tests;

public sealed class NvidiaFrucLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fruc-locator-{Guid.NewGuid():N}");

    public NvidiaFrucLocatorTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void The_newest_sdk_under_a_root_is_found()
    {
        var older = Library("Optical_Flow_SDK_4.0.0");
        var newer = Library("Optical_Flow_SDK_5.0.7");

        Assert.Equal(newer, NvidiaFrucLocator.Find(null, [Path.Combine(_root, "missing"), _root]));
        Assert.NotEqual(older, newer);
    }

    [Fact]
    public void A_configured_folder_or_library_path_comes_first()
    {
        _ = Library("Optical_Flow_SDK_5.0.7");
        var own = Path.Combine(_root, "own");
        Directory.CreateDirectory(own);
        File.WriteAllBytes(Path.Combine(own, "NvOFFRUC.dll"), []);

        Assert.Equal(own, NvidiaFrucLocator.Find(own, [_root]));
        Assert.Equal(own, NvidiaFrucLocator.Find(Path.Combine(own, "NvOFFRUC.dll"), [_root]));
    }

    [Fact]
    public void Nothing_is_found_without_the_library()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Optical_Flow_SDK_5.0.7", NvidiaFrucLocator.BinaryFolder));

        Assert.Null(NvidiaFrucLocator.Find(Path.Combine(_root, "nowhere"), [_root]));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private string Library(string sdk)
    {
        var folder = Path.Combine(_root, sdk, NvidiaFrucLocator.BinaryFolder);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "NvOFFRUC.dll"), []);

        return folder;
    }
}
