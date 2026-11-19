// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Graphics.Sokol;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Xunit.Abstractions;

namespace Phantom.Relay.App.Tests.Pictures;

/// <summary>
/// The upscaler on this machine's GPU through the engine's own window and device: a 2560 x 1440 picture drawn into
/// its target, FSR 1's EASU to 3840 x 2160 and RCAS into a target in the window's format, each pass timed by the
/// device; against the picture drawn bilinearly at 3840 x 2160 as before. Also counts what the frame's upscaling
/// allocates on the managed heap after a warm-up (nothing is allowed).
/// </summary>
[Trait("Category", "Live")]
public sealed class StreamUpscalerLiveTests
{
    private readonly ITestOutputHelper _output;

    public StreamUpscalerLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Fsr_from_1440p_to_4k_costs_its_passes_and_allocates_nothing()
    {
        var window = new SokolWindowHost();
        var device = new SokolDevice(SokolLimits.Default, NullLogger<SokolDevice>.Instance);
        var application = new UpscaleBench(device, window);
        var driver = new ApplicationDriver(application, new InputFrame(InputLimits.Default), new NoGamepads());

        SokolRunner.Run(driver, new SokolWindow(640, 360, "Relay upscaler bench") { UnpacedHiddenFrames = true }, SokolLimits.Default);

        Assert.True(application.Problem is null, application.Problem);
        _output.WriteLine(application.Report());
        Assert.Equal(0, application.Allocated);
    }
}
