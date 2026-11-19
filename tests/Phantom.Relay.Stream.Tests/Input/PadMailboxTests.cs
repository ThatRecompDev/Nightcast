// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Stream.Tests.Input;

public sealed class PadMailboxTests
{
    private static readonly XboxPadState Pressed = new(XboxButtons.A, 0f, 0f, 0f, 0f, 0f, 0f);
    private static readonly XboxPadState Moved = new(XboxButtons.None, 0.5f, -0.25f, 0f, 0f, 0f, 1f);

    [Fact]
    public void Nothing_waits_before_a_pad_is_posted()
    {
        var pads = new PadMailbox();

        Assert.False(pads.TryTake(out _, out _));
    }

    [Fact]
    public void The_newest_state_wins_when_two_arrive_before_the_network_thread_looks()
    {
        var pads = new PadMailbox();
        pads.Post(0, Pressed);
        pads.Post(0, Moved);

        bool taken = pads.TryTake(out byte index, out var pad);

        Assert.True(taken);
        Assert.Equal(0, index);
        Assert.Equal(Moved, pad);
        Assert.False(pads.TryTake(out _, out _));
    }

    [Fact]
    public void A_change_after_a_take_is_taken_again()
    {
        var pads = new PadMailbox();
        pads.Post(0, Pressed);
        pads.TryTake(out _, out _);

        pads.Post(0, Moved);

        Assert.True(pads.TryTake(out _, out var pad));
        Assert.Equal(Moved, pad);
    }

    [Fact]
    public void Each_pad_keeps_its_own_state_lowest_index_first()
    {
        var pads = new PadMailbox();
        pads.Post(2, Moved);
        pads.Post(0, Pressed);

        pads.TryTake(out byte first, out var firstPad);
        pads.TryTake(out byte second, out var secondPad);

        Assert.Equal(0, first);
        Assert.Equal(Pressed, firstPad);
        Assert.Equal(2, second);
        Assert.Equal(Moved, secondPad);
    }

    [Fact]
    public void A_state_that_could_not_be_sent_goes_back_unless_a_newer_one_came()
    {
        var pads = new PadMailbox();
        pads.Post(1, Pressed);
        pads.TryTake(out _, out var unsent);

        pads.Restore(1, unsent);

        Assert.True(pads.TryTake(out _, out var again));
        Assert.Equal(Pressed, again);

        pads.Post(1, Moved);
        pads.Restore(1, Pressed);

        Assert.True(pads.TryTake(out _, out var newest));
        Assert.Equal(Moved, newest);
    }

    [Fact]
    public void A_pad_beyond_the_fourth_is_refused()
    {
        var pads = new PadMailbox();

        Assert.Throws<ArgumentOutOfRangeException>(() => pads.Post(PadMailbox.MaxPads, Pressed));
    }

    [Fact]
    public void States_posted_from_another_thread_all_arrive_as_the_newest()
    {
        var pads = new PadMailbox();
        var last = new XboxPadState(XboxButtons.None, 1f, 0f, 0f, 0f, 0f, 0f);

        var writer = new Thread(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                pads.Post(0, new XboxPadState(XboxButtons.None, i / 1000f, 0f, 0f, 0f, 0f, 0f));
            }

            pads.Post(0, last);
        });
        writer.Start();
        writer.Join();

        Assert.True(pads.TryTake(out _, out var pad));
        Assert.Equal(last, pad);
    }
}
