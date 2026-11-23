// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// Ending the stream from the pad alone, for a handheld without an Escape key: View and Menu held together for
/// <see cref="Hold"/>. A short press of both stays the game's.
/// </summary>
public sealed class QuitChord
{
    /// <summary>NOT DATA: how long View and Menu are held together before the stream ends.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(2);

    private const XboxButtons Chord = XboxButtons.View | XboxButtons.Menu;

    private TimeSpan _since;
    private bool _holding;

    /// <summary>True once the chord has been held for <see cref="Hold"/> up to <paramref name="now"/>.</summary>
    [HotPath]
    public bool Held(in XboxPadState pad, TimeSpan now)
    {
        if ((pad.Buttons & Chord) != Chord)
        {
            _holding = false;

            return false;
        }

        if (!_holding)
        {
            _holding = true;
            _since = now;
        }

        return now - _since >= Hold;
    }
}
