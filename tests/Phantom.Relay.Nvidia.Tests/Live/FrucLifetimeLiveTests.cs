// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Tests.Live;
using Xunit.Abstractions;

namespace Phantom.Relay.Nvidia.Tests.Live;

/// <summary>
/// FRUC's life as F4 drives it: one generator per device, paused and resumed (<see cref="FrucGenerator.Restart"/>);
/// made and torn down again on fresh devices (a new stream); and the library's limit pinned, so a newer SDK that lifts
/// it shows up here: a second FRUC on a device whose first is gone cannot share its fence.
/// </summary>
[Trait("Category", "Live")]
[Collection(FrucOneAtATime.Name)]
public sealed class FrucLifetimeLiveTests
{
    private const int Width = ArtifactScene.Width;
    private const int Height = ArtifactScene.Height;
    private const int Rounds = 3;

    private readonly TestOutputLogger _log;

    public FrucLifetimeLiveTests(ITestOutputHelper output)
    {
        _log = new TestOutputLogger(output);
    }

    [Fact]
    public void A_paused_generator_starts_again_from_a_first_frame()
    {
        using var device = FrucSdk.NvidiaDevice();
        var first = device.CreateRgba(Width, Height, ArtifactScene.Scene(0));
        var second = device.CreateRgba(Width, Height, ArtifactScene.Scene(16));
        using var fruc = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Rgba, _log, out var problem);
        Assert.True(fruc is not null, problem);

        for (var round = 0; round < Rounds; round++)
        {
            fruc.Restart();
            Assert.Equal(0, fruc.Submit(first, 0, 2));
            Assert.True(fruc.Submit(second, 0, 2) == 1, $"round {round}: {fruc.Failure}");
            _ = device.ReadRgba(fruc.Output(0), Width, Height);
        }
    }

    [Fact]
    public void FRUC_is_made_and_torn_down_again_on_fresh_devices()
    {
        for (var round = 0; round < Rounds; round++)
        {
            using var device = FrucSdk.NvidiaDevice();
            var first = device.CreateRgba(Width, Height, ArtifactScene.Scene(0));
            var second = device.CreateRgba(Width, Height, ArtifactScene.Scene(16));
            using var fruc = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Rgba, _log, out var problem);
            Assert.True(fruc is not null, $"round {round}: {problem}");

            Assert.Equal(0, fruc.Submit(first, 0, 2));
            Assert.True(fruc.Submit(second, 0, 2) == 1, $"round {round}: {fruc.Failure}");
            _ = device.ReadRgba(fruc.Output(0), Width, Height);
        }
    }

    [Fact]
    public void A_second_FRUC_on_one_device_is_refused_with_a_reason()
    {
        using var device = FrucSdk.NvidiaDevice();
        var frame = device.CreateRgba(Width, Height, ArtifactScene.Scene(0));

        using (var fruc = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Rgba, _log, out var problem))
        {
            Assert.True(fruc is not null, problem);
            _ = fruc.Submit(frame, 0, 2);
        }

        using var again = FrucGenerator.TryCreate(FrucSdk.Folder(), device.Device, Width, Height, FrucSurfaceFormat.Rgba, _log, out var refusal);

        Assert.Null(again);
        Assert.Contains("NvOFFRUCRegisterResource", refusal, StringComparison.Ordinal);
    }
}
