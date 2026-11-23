// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Desktop.Stills;

namespace Phantom.Relay.Desktop.Tests.Stills;

public sealed class NoticeBoardTests
{
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_notice_shows_for_its_while_as_the_same_string()
    {
        var board = new NoticeBoard(_clock);
        board.Post("screenshot saved");
        var first = board.Current;

        _clock.Advance(NoticeBoard.ShownFor - TimeSpan.FromMilliseconds(1));

        Assert.Same(first, board.Current);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(board.Current);
    }

    [Fact]
    public void A_new_notice_replaces_the_one_before_with_its_own_while()
    {
        var board = new NoticeBoard(_clock);
        board.Post("taking a screenshot");
        board.Post("screenshot saved", NoticeBoard.ShownFor * 2);

        _clock.Advance(NoticeBoard.ShownFor);

        Assert.Equal("screenshot saved", board.Current);
    }
}
