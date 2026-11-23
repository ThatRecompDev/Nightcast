// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.App.Tests.Setup;

public sealed class ScreenConsoleChoiceTests
{
    private static readonly XboxConsole[] Consoles =
    [
        new("F1", "Living room", "XboxSeriesX", "On", true),
        new("F2", "Bedroom", "XboxSeriesS", "ConnectedStandby", true),
        new("F3", "Office", "XboxOne", "On", false),
    ];

    private readonly ScreenConsoleChoice _choice = new();

    [Fact]
    public void Nothing_is_offered_until_the_setup_asks()
    {
        Assert.False(_choice.TryOptions(out var names, out _));
        Assert.Empty(names);
    }

    [Fact]
    public async Task The_highlight_moves_and_wraps_and_the_highlighted_console_is_picked()
    {
        var chosen = _choice.ChooseAsync(Consoles, CancellationToken.None);
        _choice.Move(-1);
        _choice.TryOptions(out var names, out var highlighted);

        Assert.Equal(["Living room", "Bedroom", "Office"], names);
        Assert.Equal(2, highlighted);

        _choice.Move(1);
        _choice.Move(1);
        _choice.Pick();

        Assert.Equal(1, await chosen);
        Assert.False(_choice.TryOptions(out _, out _));
    }

    [Fact]
    public async Task A_cancelled_choice_picks_none()
    {
        var chosen = _choice.ChooseAsync(Consoles, CancellationToken.None);

        _choice.Cancel();

        Assert.Null(await chosen);
    }

    [Fact]
    public async Task A_setup_cancelled_from_outside_ends_the_wait()
    {
        using var cancellation = new CancellationTokenSource();
        var chosen = _choice.ChooseAsync(Consoles, cancellation.Token);

        await cancellation.CancelAsync();

        Assert.Null(await chosen);
    }
}
