// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Xbox.Channels;

namespace Phantom.Relay.Xbox.Tests.Channels;

public sealed class PadAnnouncementTests
{
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Nothing_is_announced_before_the_game_takes_input()
    {
        var pad = new PadAnnouncement(_clock);
        pad.Described();

        Assert.Equal(PadStep.None, pad.Next());
        Assert.False(pad.ReportsMayFlow);
    }

    [Fact]
    public void The_pad_is_announced_once_the_console_completes_the_un_constrain()
    {
        var pad = new PadAnnouncement(_clock);
        pad.Described();
        pad.Unconstrained();

        Assert.Equal(PadStep.Announce, pad.Next());
        Assert.True(pad.ReportsMayFlow);
        Assert.Equal(PadStep.None, pad.Next());
    }

    [Fact]
    public void Without_a_completion_the_pad_goes_after_the_fallback()
    {
        var pad = new PadAnnouncement(_clock);
        pad.Described();

        _clock.Advance(PadAnnouncement.ReadyFallback - TimeSpan.FromMilliseconds(1));
        Assert.Equal(PadStep.None, pad.Next());

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(PadStep.Announce, pad.Next());
    }

    [Fact]
    public void An_unanswered_pad_is_announced_again_until_the_limit()
    {
        var pad = new PadAnnouncement(_clock);
        pad.Unconstrained();
        Assert.Equal(PadStep.Announce, pad.Next());

        for (int again = 1; again < PadAnnouncement.MaxAnnouncements; again++)
        {
            _clock.Advance(PadAnnouncement.AnswerWait - TimeSpan.FromMilliseconds(1));
            Assert.Equal(PadStep.None, pad.Next());

            _clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(PadStep.Reannounce, pad.Next());
        }

        _clock.Advance(PadAnnouncement.AnswerWait);
        Assert.Equal(PadStep.None, pad.Next());
        Assert.Equal(PadAnnouncement.MaxAnnouncements, pad.Announcements);
    }

    [Fact]
    public void An_answered_pad_is_not_announced_again()
    {
        var pad = new PadAnnouncement(_clock);
        pad.Unconstrained();
        Assert.Equal(PadStep.Announce, pad.Next());

        pad.Answered();
        _clock.Advance(PadAnnouncement.AnswerWait * 2);

        Assert.Equal(PadStep.None, pad.Next());
        Assert.True(pad.ReportsMayFlow);
    }
}
