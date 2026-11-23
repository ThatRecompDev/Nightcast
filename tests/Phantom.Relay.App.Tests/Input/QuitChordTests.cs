// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Tests.Input;

public sealed class QuitChordTests
{
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);
    private static readonly XboxPadState Both = Pad(XboxButtons.View | XboxButtons.Menu | XboxButtons.A);

    [Fact]
    public void View_and_menu_held_together_end_the_stream_only_once_held_long_enough()
    {
        var chord = new QuitChord();

        Assert.False(chord.Held(in Both, Start));
        Assert.False(chord.Held(in Both, Start + QuitChord.Hold - TimeSpan.FromMilliseconds(1)));
        Assert.True(chord.Held(in Both, Start + QuitChord.Hold));
    }

    [Fact]
    public void Letting_go_starts_the_hold_again_and_one_button_alone_never_counts()
    {
        var chord = new QuitChord();
        var view = Pad(XboxButtons.View);

        chord.Held(in Both, Start);
        Assert.False(chord.Held(in view, Start + QuitChord.Hold));
        Assert.False(chord.Held(in Both, Start + QuitChord.Hold));
        Assert.False(chord.Held(in view, Start + QuitChord.Hold + QuitChord.Hold));
    }

    private static XboxPadState Pad(XboxButtons buttons)
    {
        return new XboxPadState(buttons, 0f, 0f, 0f, 0f, 0f, 0f);
    }
}
