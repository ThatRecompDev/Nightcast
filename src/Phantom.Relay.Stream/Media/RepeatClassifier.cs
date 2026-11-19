// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Tells, for each access unit, whether it carries a new picture or repeats the one before. The console streams 60
/// pictures a second whatever the game draws, so a 30 fps game's every other unit is a repeat: the console codes the same
/// picture again, refined: about half the bytes of the new picture before it (measured on GTA VI). A unit is small at
/// most <see cref="RepeatSettings.RepeatBytes"/> or at most <see cref="RepeatSettings.RepeatPercent"/> % of the unit
/// before (<see cref="RepeatSettings.HoldPercent"/> % once the half-rate rhythm is found). Size alone is fooled by a quiet
/// scene, whose new pictures are small too, so the classifier follows the cadence:
/// <list type="bullet">
/// <item><b>Full</b> (the start): every unit is new. After <see cref="RepeatSettings.LockUnits"/> units in a row that
/// alternate small (at most <see cref="RepeatSettings.RepeatBytes"/>) and large, it moves to Half.</item>
/// <item><b>Half</b>: units alternate between the new phase and the repeat phase. A unit in the new phase is new
/// whatever its size (a quiet scene's picture); one in the repeat phase is a repeat when small. A large one there is
/// new: alone it is a slip of the rhythm (the next small unit in the new phase moves the phase on by one);
/// <see cref="RepeatSettings.UnlockUnits"/> in a row mean the game now draws every picture, back to Full. A unit in the
/// new phase at most <see cref="RepeatSettings.HoldPercent"/> % of the unit before is a <b>held</b> picture: the game
/// dropped below 30 fps (25 fps sends N r N r r ...) and the console refined the same picture once more, so it is a
/// repeat and the phase moves on by one, as on a slip; frame generation is never fed one picture twice. Only after a
/// unit larger than <see cref="RepeatSettings.RepeatBytes"/>: a quiet scene's tiny units vary too much for a share of
/// them to mean anything, and a picture held in a quiet scene leaves nothing for an interpolation to get wrong.</item>
/// </list>
/// A key frame is always new. A quiet scene therefore never turns a 30 fps game's new pictures into repeats, nor a
/// 60 fps game's small pictures into repeats. One thread at a time; allocates nothing.
/// </summary>
public sealed class RepeatClassifier
{
    private readonly RepeatSettings _settings;
    private int _alternating;
    private bool _lastSmall;
    private bool _nextIsNewPhase;
    private bool _slipped;
    private int _breaks;
    private long _previousBytes;

    public RepeatClassifier(RepeatSettings settings)
    {
        _settings = settings;
    }

    public PictureCadence Cadence { get; private set; }

    /// <summary>Units taken as new so far.</summary>
    public long News { get; private set; }

    /// <summary>Units taken as repeats so far.</summary>
    public long Repeats { get; private set; }

    /// <summary>What an access unit of <paramref name="bytes"/> bytes carries; <paramref name="key"/> for a key frame.</summary>
    [HotPath]
    public PictureKind Classify(int bytes, bool key)
    {
        var small = !key && (bytes <= _settings.RepeatBytes || (long)bytes * RepeatSettings.Percent <= _previousBytes * SmallPercent());
        var held = !key && _previousBytes > _settings.RepeatBytes && (long)bytes * RepeatSettings.Percent <= _previousBytes * _settings.HoldPercent;
        _previousBytes = bytes;
        PictureKind kind;

        if (Cadence == PictureCadence.Half)
        {
            kind = InHalf(small, held);
        }
        else
        {
            kind = InFull(small);
        }

        if (kind == PictureKind.Repeat)
        {
            Repeats++;
        }
        else
        {
            News++;
        }

        return kind;
    }

    // The share of the unit before that counts as small: strict to find the half-rate rhythm, looser to keep it.
    private int SmallPercent()
    {
        if (Cadence == PictureCadence.Half)
        {
            return _settings.HoldPercent;
        }

        return _settings.RepeatPercent;
    }

    // Every unit is new until the sizes alternate long enough; the unit that completes the run is classed by the new rhythm.
    private PictureKind InFull(bool small)
    {
        if (_alternating > 0 && small != _lastSmall)
        {
            _alternating++;
        }
        else
        {
            _alternating = 1;
        }

        _lastSmall = small;

        if (_alternating < _settings.LockUnits)
        {
            return PictureKind.New;
        }

        Cadence = PictureCadence.Half;
        _nextIsNewPhase = small;
        _breaks = 0;
        _slipped = false;

        if (small)
        {
            return PictureKind.Repeat;
        }

        return PictureKind.New;
    }

    private PictureKind InHalf(bool small, bool held)
    {
        var newPhase = _nextIsNewPhase;
        _nextIsNewPhase = !newPhase;

        if (newPhase)
        {
            return InNewPhase(small, held);
        }

        if (small)
        {
            _breaks = 0;
            _slipped = false;

            return PictureKind.Repeat;
        }

        _breaks++;
        _slipped = true;

        if (_breaks >= _settings.UnlockUnits)
        {
            Cadence = PictureCadence.Full;
            _alternating = 0;
        }

        return PictureKind.New;
    }

    // A small unit right after a large one in the repeat phase: the rhythm slipped by one, so this is the repeat. A unit
    // clearly smaller than the repeat before it: the game held its picture past two units, so this repeats it again.
    // Either way the phase moves on by one: the next unit is the new phase's.
    private PictureKind InNewPhase(bool small, bool held)
    {
        var slipped = _slipped && small;
        _slipped = false;

        if (!slipped && !held)
        {
            return PictureKind.New;
        }

        _nextIsNewPhase = true;
        _breaks = 0;

        return PictureKind.Repeat;
    }
}
