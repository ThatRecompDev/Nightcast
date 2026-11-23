// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Tests.Input;

public sealed class PadMenuTests
{
    [Fact]
    public void The_d_pad_moves_a_presses_and_only_a_fresh_press_counts()
    {
        var menu = new PadMenu();
        menu.Open(XboxButtons.A);

        Assert.Null(menu.Step(XboxButtons.A));
        Assert.Null(menu.Step(XboxButtons.DPadDown));
        Assert.Equal(MenuItem.FrameGeneration, menu.Selected);

        Assert.Equal(MenuItem.FrameGeneration, menu.Step(XboxButtons.A));
        Assert.Null(menu.Step(XboxButtons.A));
        Assert.Equal(MenuItem.FrameGeneration, menu.Step(XboxButtons.DPadRight));
    }

    [Fact]
    public void Up_from_the_first_row_wraps_to_the_last_and_b_closes()
    {
        var menu = new PadMenu();
        menu.Open(XboxButtons.None);

        menu.Step(XboxButtons.DPadUp);

        Assert.Equal(MenuItem.EndStream, menu.Selected);
        Assert.Null(menu.Step(XboxButtons.B));
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void The_lines_show_the_pointer_on_the_chosen_row_with_its_value()
    {
        var menu = new PadMenu();
        menu.Open(XboxButtons.None);

        var lines = menu.Lines(StatsOn);

        Assert.StartsWith("> Stats", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("on", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("  Frame generation", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void The_screenshot_row_follows_record_and_chooses_a_screenshot()
    {
        var menu = new PadMenu();
        menu.Open(XboxButtons.None);
        var lines = menu.Lines(_ => string.Empty);
        var record = Array.FindIndex(lines, line => line.Contains("Record", StringComparison.Ordinal));

        Assert.StartsWith("  Screenshot", lines[record + 1], StringComparison.Ordinal);

        // The rows follow the title line, so the screenshot row is as many presses down as the record row's line.
        for (var step = 0; step < record; step++)
        {
            menu.Step(XboxButtons.DPadDown);
            menu.Step(XboxButtons.None);
        }

        Assert.Equal(MenuItem.Screenshot, menu.Step(XboxButtons.A));
    }

    private static string StatsOn(MenuItem item)
    {
        if (item == MenuItem.Stats)
        {
            return "on";
        }

        return string.Empty;
    }
}
