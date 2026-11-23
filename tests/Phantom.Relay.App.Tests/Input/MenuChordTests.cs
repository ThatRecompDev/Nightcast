// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Tests.Input;

public sealed class MenuChordTests
{
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);
    private static readonly XboxPadState Both = Pad(XboxButtons.View | XboxButtons.Menu);
    private static readonly XboxPadState ViewOnly = Pad(XboxButtons.View);
    private static readonly XboxPadState Rest = Pad(XboxButtons.None);

    [Fact]
    public void A_short_press_of_both_opens_the_menu_once_both_are_let_go()
    {
        var chord = new MenuChord();

        Assert.Equal(ChordStep.Held, chord.Step(in Both, Start));
        Assert.Equal(ChordStep.Held, chord.Step(in ViewOnly, Start + TimeSpan.FromMilliseconds(100)));
        Assert.Equal(ChordStep.OpenMenu, chord.Step(in Rest, Start + TimeSpan.FromMilliseconds(150)));
        Assert.Equal(ChordStep.None, chord.Step(in Rest, Start + TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void Held_long_enough_it_ends_the_stream_once_and_opens_no_menu()
    {
        var chord = new MenuChord();
        chord.Step(in Both, Start);

        Assert.Equal(ChordStep.Held, chord.Step(in Both, Start + MenuChord.QuitHold - TimeSpan.FromMilliseconds(1)));
        Assert.Equal(ChordStep.Quit, chord.Step(in Both, Start + MenuChord.QuitHold));
        Assert.Equal(ChordStep.Held, chord.Step(in Both, Start + MenuChord.QuitHold + MenuChord.QuitHold));
        Assert.Equal(ChordStep.None, chord.Step(in Rest, Start + MenuChord.QuitHold + MenuChord.QuitHold));
    }

    [Fact]
    public void One_of_the_two_alone_stays_the_games()
    {
        var chord = new MenuChord();

        Assert.Equal(ChordStep.None, chord.Step(in ViewOnly, Start));
        Assert.Equal(ChordStep.None, chord.Step(in Rest, Start + MenuChord.QuitHold));
    }

    private static XboxPadState Pad(XboxButtons buttons)
    {
        return new XboxPadState(buttons, 0f, 0f, 0f, 0f, 0f, 0f);
    }
}
