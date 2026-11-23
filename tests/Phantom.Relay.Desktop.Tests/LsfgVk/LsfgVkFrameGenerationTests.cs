// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using System.Security.Cryptography;
using Phantom.Engine.Content.Storage;
using Phantom.Relay.Desktop.LsfgVk;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkFrameGenerationTests : IDisposable
{
    private static readonly LayerCheck Loaded = new(true, "2.0.0", null);
    private static readonly TimeSpan BackgroundWait = TimeSpan.FromSeconds(10);

    private readonly TempFolder _folder = new();
    private readonly MemoryPersistentStore _store = new();
    private readonly FakeEnvironment _environment = new();
    private readonly LsfgVkFolders _folders;
    private readonly LsfgVkLossless _lossless;
    private BytesSource _source = new(FakeArchive.Release());

    public LsfgVkFrameGenerationTests()
    {
        _folders = new LsfgVkFolders(Path.Combine(_folder.Path, "lsfg-vk"));
        _lossless = new LsfgVkLossless(Path.Combine(_folder.Path, "lsfg-vk.dll"), null);
    }

    public void Dispose()
    {
        _folder.Dispose();
    }

    [Fact]
    public async Task Kept_on_it_is_downloaded_configured_and_loaded_before_the_window()
    {
        Remember(FrameGeneration.QualityDoubled with { Multiplier = 3 });
        var probe = new FakeProbe(_environment, Loaded);
        var lsfgVk = Make(probe);

        await lsfgVk.StartAsync(CancellationToken.None);

        Assert.Equal(LsfgVkState.Loaded, lsfgVk.State);
        Assert.True(lsfgVk.Running);
        Assert.True(_folders.Installed);
        Assert.Contains("multiplier = 3\n", File.ReadAllText(_folders.ConfigPath), StringComparison.Ordinal);
        Assert.Equal(_folders.ConfigPath, Assert.Single(probe.Seen)[LsfgVkEnvironment.Config]);
        Assert.Equal("lsfg-vk: LSFG x3 (quality, flow 1.00)", lsfgVk.Describe("LSFG x3 (quality, flow 1.00)"));
    }

    [Fact]
    public async Task Loaded_every_choice_is_written_for_the_layer_to_take_live_and_kept()
    {
        Remember(FrameGeneration.QualityDoubled);
        var lsfgVk = Make(new FakeProbe(_environment, Loaded));
        await lsfgVk.StartAsync(CancellationToken.None);

        lsfgVk.Use(FrameGeneration.Off);

        Assert.False(lsfgVk.Running);
        Assert.Contains("multiplier = 1\n", File.ReadAllText(_folders.ConfigPath), StringComparison.Ordinal);
        Assert.Equal("lsfg-vk: off", lsfgVk.Describe("off"));

        lsfgVk.Use(FrameGeneration.QualityDoubled with { Multiplier = 4 });

        Assert.True(lsfgVk.Running);
        Assert.Contains("multiplier = 4\n", File.ReadAllText(_folders.ConfigPath), StringComparison.Ordinal);
        Assert.Equal(4, new LsfgVkChoiceStore(_store).Load().Multiplier);
        Assert.Equal(1, _source.Downloads);
    }

    [Fact]
    public async Task Kept_off_nothing_is_touched_and_turning_it_on_prepares_it_then_asks_for_a_restart()
    {
        var lsfgVk = Make(new FakeProbe(_environment, Loaded));
        await lsfgVk.StartAsync(CancellationToken.None);

        Assert.Equal(LsfgVkState.Off, lsfgVk.State);
        Assert.Equal("off (lsfg-vk)", lsfgVk.Describe("off"));
        Assert.Empty(_environment.Values);
        Assert.Equal(0, _source.Downloads);

        lsfgVk.Use(FrameGeneration.QualityDoubled);
        await Settled(lsfgVk);

        Assert.True(lsfgVk.RestartRequested);
        Assert.False(lsfgVk.Running);
        Assert.True(_folders.Installed);
        Assert.Contains("multiplier = 2\n", File.ReadAllText(_folders.ConfigPath), StringComparison.Ordinal);
        Assert.Equal("lsfg-vk: ready, Nightcast restarts to load it", lsfgVk.Describe("LSFG x2"));
    }

    [Fact]
    public async Task A_loader_that_does_not_list_it_is_given_the_data_folder_and_asked_again()
    {
        Remember(FrameGeneration.QualityDoubled);
        var probe = new FakeProbe(_environment, new LayerCheck(false, null, null), Loaded);
        var lsfgVk = Make(probe);

        await lsfgVk.StartAsync(CancellationToken.None);

        Assert.Equal(LsfgVkState.Loaded, lsfgVk.State);
        Assert.False(probe.Seen[0].ContainsKey(LsfgVkEnvironment.DataFolders));
        Assert.StartsWith(_folders.DataFolder + ":", probe.Seen[1][LsfgVkEnvironment.DataFolders], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_layer_that_is_listed_but_does_not_load_says_so_and_asks_for_no_restart()
    {
        Remember(FrameGeneration.QualityDoubled);
        var lsfgVk = Make(new FakeProbe(_environment, new LayerCheck(true, null, null)));
        await lsfgVk.StartAsync(CancellationToken.None);

        lsfgVk.Use(FrameGeneration.QualityDoubled with { Multiplier = 3 });

        Assert.Equal(LsfgVkState.NotLoaded, lsfgVk.State);
        Assert.False(lsfgVk.Running);
        Assert.False(lsfgVk.RestartRequested);
        Assert.StartsWith("lsfg-vk did not load: the Vulkan loader lists the layer but it did not start", lsfgVk.Describe("LSFG x3"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_Lossless_Scaling_nothing_is_downloaded_and_the_row_says_why()
    {
        Remember(FrameGeneration.QualityDoubled);
        var lsfgVk = new LsfgVkFrameGeneration(_folders, new LsfgVkLossless(null, "Lossless Scaling was not found in Steam"), Installer(), _environment, new FakeProbe(_environment, Loaded), new LsfgVkChoiceStore(_store));

        await lsfgVk.StartAsync(CancellationToken.None);
        lsfgVk.Use(FrameGeneration.QualityDoubled);

        Assert.Equal(LsfgVkState.Unavailable, lsfgVk.State);
        Assert.Equal("lsfg-vk cannot run: Lossless Scaling was not found in Steam", lsfgVk.Describe("LSFG x2"));
        Assert.Equal(0, _source.Downloads);
        Assert.Empty(_environment.Values);
    }

    [Fact]
    public async Task A_download_that_is_not_the_pinned_build_fails_and_turning_it_on_again_retries()
    {
        Remember(FrameGeneration.QualityDoubled);
        var installer = new LsfgVkInstaller(_folders, _source, new CopyDecompressor(), LsfgVkRelease.Sha256);
        var lsfgVk = new LsfgVkFrameGeneration(_folders, _lossless, installer, _environment, new FakeProbe(_environment, Loaded), new LsfgVkChoiceStore(_store));

        await lsfgVk.StartAsync(CancellationToken.None);

        Assert.Equal(LsfgVkState.Failed, lsfgVk.State);
        Assert.StartsWith("lsfg-vk failed: the download is not the official lsfg-vk 2.0.0", lsfgVk.Describe("LSFG x2"), StringComparison.Ordinal);
        Assert.Empty(_environment.Values);

        lsfgVk.Use(FrameGeneration.QualityDoubled);
        await Settled(lsfgVk);

        Assert.Equal(LsfgVkState.Failed, lsfgVk.State);
        Assert.Equal(2, _source.Downloads);
    }

    [Fact]
    public async Task A_start_that_crashed_with_the_layer_turns_it_off_with_its_error_and_loads_nothing()
    {
        Remember(FrameGeneration.QualityDoubled);
        await Make(new FakeProbe(_environment, Loaded)).StartAsync(CancellationToken.None);
        File.WriteAllText(_folders.LogPath, "(lsfg-vk) [ERROR]: An error occured while initializing the lsfg-vk swapchain:\n(lsfg-vk) [ERROR]: - vk::PhysicalDevice::createDeviceUnique: -7\n");

        // That start never confirmed a stream: the next one leaves the layer out.
        var probe = new FakeProbe(_environment, Loaded);
        var next = Make(probe);
        await next.StartAsync(CancellationToken.None);

        Assert.Equal(LsfgVkState.Failed, next.State);
        Assert.False(next.Remembered.IsOn);
        Assert.False(new LsfgVkChoiceStore(_store).Load().IsOn);
        Assert.Empty(probe.Seen);
        Assert.Contains("createDeviceUnique: -7", next.Describe("LSFG x2"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_start_whose_stream_ran_loads_the_layer_again_next_time()
    {
        Remember(FrameGeneration.QualityDoubled);
        var first = Make(new FakeProbe(_environment, Loaded));
        await first.StartAsync(CancellationToken.None);
        first.Confirm();

        var next = Make(new FakeProbe(_environment, Loaded));
        await next.StartAsync(CancellationToken.None);

        Assert.Equal(LsfgVkState.Loaded, next.State);
    }

    private void Remember(FrameGeneration settings)
    {
        new LsfgVkChoiceStore(_store).Save(settings);
    }

    private LsfgVkFrameGeneration Make(FakeProbe probe)
    {
        return new LsfgVkFrameGeneration(_folders, _lossless, Installer(), _environment, probe, new LsfgVkChoiceStore(_store));
    }

    private LsfgVkInstaller Installer()
    {
        var archive = FakeArchive.Release();
        _source = new BytesSource(archive);

        return new LsfgVkInstaller(_folders, _source, new CopyDecompressor(), Convert.ToHexStringLower(SHA256.HashData(archive)));
    }

    /// <summary>Until the background preparation ended.</summary>
    private static async Task Settled(LsfgVkFrameGeneration lsfgVk)
    {
        var clock = Stopwatch.StartNew();

        while (lsfgVk.State == LsfgVkState.Preparing && clock.Elapsed < BackgroundWait)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        Assert.NotEqual(LsfgVkState.Preparing, lsfgVk.State);
    }
}
