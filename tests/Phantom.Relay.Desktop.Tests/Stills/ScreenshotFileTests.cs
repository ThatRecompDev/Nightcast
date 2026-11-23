// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Places;
using Phantom.Relay.Desktop.Stills;

namespace Phantom.Relay.Desktop.Tests.Stills;

public sealed class ScreenshotFileTests
{
    private static readonly DateTimeOffset Taken = new(2026, 11, 23, 21, 14, 3, TimeSpan.Zero);

    [Fact]
    public void A_screenshot_is_named_by_its_local_time_in_the_nightcast_folder()
    {
        var file = ScreenshotFile.For("Pictures", Taken, _ => false);

        Assert.Equal(Path.Combine("Pictures", "Nightcast", "2026-11-23 21-14-03.png"), file);
    }

    [Fact]
    public void A_second_screenshot_of_the_same_second_gets_a_number()
    {
        var first = Path.Combine("Pictures", "Nightcast", "2026-11-23 21-14-03.png");

        var file = ScreenshotFile.For("Pictures", Taken, path => path == first);

        Assert.Equal(Path.Combine("Pictures", "Nightcast", "2026-11-23 21-14-03 (2).png"), file);
    }

    [Fact]
    public void The_window_shows_the_file_below_the_ellipsis()
    {
        var file = Path.Combine("Pictures", "Nightcast", "2026-11-23 21-14-03.png");

        Assert.Equal(Path.Combine("…", "Nightcast", "2026-11-23 21-14-03.png"), ScreenshotFile.Short(file));
    }

    [Fact]
    public void A_user_folder_is_the_xdg_variables_else_the_systems_else_the_homes()
    {
        Assert.Equal("xdg", UserFolders.Pick("xdg", "system", "home", "Pictures"));
        Assert.Equal("system", UserFolders.Pick(null, "system", "home", "Pictures"));
        Assert.Equal(Path.Combine("home", "Pictures"), UserFolders.Pick(string.Empty, string.Empty, "home", "Pictures"));
    }
}
