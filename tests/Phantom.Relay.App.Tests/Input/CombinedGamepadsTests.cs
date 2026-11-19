// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.App.Input;

namespace Phantom.Relay.App.Tests.Input;

public sealed class CombinedGamepadsTests
{
    private static readonly GamepadState XboxPad = new(true, GamepadButtons.South, Vector2.Zero, Vector2.Zero, 0f, 0f, default);
    private static readonly GamepadState PlayStationPad = new(true, GamepadButtons.East, Vector2.Zero, Vector2.Zero, 0f, 0f, default);

    [Fact]
    public void The_second_sources_pads_fill_the_free_slots_in_order()
    {
        var xbox = new SlotPads(new Dictionary<int, GamepadState> { [0] = XboxPad, [2] = XboxPad });
        var playStation = new SlotPads(new Dictionary<int, GamepadState> { [0] = PlayStationPad, [1] = PlayStationPad });
        var combined = new CombinedGamepads(xbox, playStation, 4);
        var pads = new GamepadState[4];

        combined.Poll(pads);

        Assert.Equal(XboxPad, pads[0]);
        Assert.Equal(PlayStationPad, pads[1]);
        Assert.Equal(XboxPad, pads[2]);
        Assert.Equal(PlayStationPad, pads[3]);
    }

    [Fact]
    public void Feedback_and_family_go_to_the_source_that_owns_the_slot()
    {
        var xbox = new SlotPads(new Dictionary<int, GamepadState> { [0] = XboxPad }) { Kind = GamepadFamily.Xbox };
        var playStation = new SlotPads(new Dictionary<int, GamepadState> { [0] = PlayStationPad }) { Kind = GamepadFamily.PlayStation };
        var combined = new CombinedGamepads(xbox, playStation, 4);
        combined.Poll(new GamepadState[4]);

        Assert.True(combined.TrySetRumble(1, 0.5f, 0.25f));
        Assert.True(combined.TrySetLight(0, 1f, 0f, 0f));

        Assert.Equal(0, playStation.LastRumbleSlot);
        Assert.Equal(0, xbox.LastLightSlot);
        Assert.Equal(GamepadFamily.PlayStation, combined.Family(1));
        Assert.Equal(GamepadFamily.Xbox, combined.Family(0));
    }

    [Fact]
    public void Pads_beyond_the_free_slots_are_left_out()
    {
        var full = new Dictionary<int, GamepadState> { [0] = XboxPad, [1] = XboxPad, [2] = XboxPad, [3] = XboxPad };
        var combined = new CombinedGamepads(new SlotPads(full), new SlotPads(new Dictionary<int, GamepadState> { [0] = PlayStationPad }), 4);
        var pads = new GamepadState[4];

        combined.Poll(pads);

        Assert.All(pads, static pad => Assert.Equal(XboxPad, pad));
        Assert.Equal(GamepadFamily.Unknown, combined.Family(3));
    }
}
