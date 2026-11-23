// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// Relay's own chord on the pad, for a handheld without a keyboard: View and Menu pressed together. Let go within
/// <see cref="QuitHold"/> it opens Relay's menu; held that long it ends the stream. From the moment both are down until
/// both are up again the chord is Relay's (<see cref="ChordStep.Held"/>): the console gets the pad at rest, so neither
/// button reaches the game. One of the two alone stays the game's.
/// </summary>
public sealed class MenuChord
{
    /// <summary>NOT DATA: how long View and Menu are held together before the stream ends.</summary>
    public static readonly TimeSpan QuitHold = TimeSpan.FromSeconds(2);

    private const XboxButtons Chord = XboxButtons.View | XboxButtons.Menu;

    private TimeSpan _since;
    private bool _holding;
    private bool _quit;

    /// <summary>What the chord does in a frame with <paramref name="pad"/> at <paramref name="now"/>.</summary>
    [HotPath]
    public ChordStep Step(in XboxPadState pad, TimeSpan now)
    {
        var held = pad.Buttons & Chord;

        if (!_holding)
        {
            if (held != Chord)
            {
                return ChordStep.None;
            }

            _holding = true;
            _quit = false;
            _since = now;
        }

        if (held != XboxButtons.None)
        {
            return Holding(now);
        }

        // Both let go: the chord ends, with the menu unless it already ended the stream.
        _holding = false;

        if (_quit)
        {
            return ChordStep.None;
        }

        return ChordStep.OpenMenu;
    }

    [HotPath]
    private ChordStep Holding(TimeSpan now)
    {
        if (!_quit && now - _since >= QuitHold)
        {
            _quit = true;

            return ChordStep.Quit;
        }

        return ChordStep.Held;
    }
}
