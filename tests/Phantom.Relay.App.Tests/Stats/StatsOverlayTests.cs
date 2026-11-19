// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Engine.Testing.Graphics;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App.Stats;

namespace Phantom.Relay.App.Tests.Stats;

public sealed class StatsOverlayTests
{
    [Theory]
    [InlineData(1f, 1080, 14f)]
    [InlineData(1.5f, 2160, 21f)]
    [InlineData(2f, 720, 18f)]
    [InlineData(1f, 360, 11f)]
    [InlineData(0f, 1080, 14f)]
    public void The_text_grows_with_the_windows_scale_and_stays_small_in_a_small_window(float dpiScale, int height, float em)
    {
        Assert.Equal(em, StatsOverlay.EmFor(dpiScale, height));
    }

    [Fact]
    public void Without_a_font_the_block_draws_nothing_and_says_so_once()
    {
        var (device, overlay, log) = Made(new NoSystemFonts());

        overlay.Initialize();
        overlay.Show(["Streaming 1920x1080"]);
        overlay.Draw();
        overlay.Show(["Streaming 1920x1080"]);
        overlay.Draw();

        Assert.False(overlay.Available);
        Assert.Equal(1, log.Count(LogLevel.Warning));
        Assert.DoesNotContain(device.Calls, call => call.Kind == GpuCallKind.Draw);
        overlay.Cleanup();
    }

    [Fact]
    public void A_font_file_the_reader_refuses_counts_as_no_font()
    {
        var (_, overlay, log) = Made(new UnreadableFonts());

        overlay.Initialize();

        Assert.False(overlay.Available);
        Assert.Equal(1, log.Count(LogLevel.Warning));
        overlay.Cleanup();
    }

    private static (RecordingGraphicsDevice Device, StatsOverlay Overlay, CountingLogger Log) Made(ISystemFonts fonts)
    {
        var device = new RecordingGraphicsDevice(RecordingGraphicsDevice.WebGpu);
        var log = new CountingLogger();

        return (device, new StatsOverlay(device, new FixedWindow(1920, 1080, 1f), fonts, log), log);
    }
}
