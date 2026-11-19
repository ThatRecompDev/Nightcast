// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// How <see cref="RepeatClassifier"/> tells a repeated picture from a new one by an access unit's encoded size, and how
/// long a rhythm must hold before it is trusted or dropped.
/// </summary>
/// <param name="RepeatBytes">
/// The largest access unit that may be a repeat. The encoder codes a picture identical to the one before as a P-frame
/// whose every macroblock is skipped: a few hundred bytes at 1440p, where a new picture of a game in motion is tens of
/// kilobytes (the owner's observation of the console's stream, 2026-11-19; not yet measured on a long capture, so the
/// value has room both ways). A new picture of a quiet scene can be as small; the cadence, not the size alone, decides
/// those.
/// </param>
/// <param name="RepeatPercent">
/// A unit at most this percentage of the unit before it is small too, whatever its size. Measured live on GTA VI at
/// 30 fps (2026-11-19): the console does not skip a repeat, it refines the same picture, so the units alternate about
/// 40 KB (the new picture) and 20 KB (its repeat), the repeat mostly at 40 to 60 % of the unit before, at times 67 %.
/// This share finds the rhythm.
/// </param>
/// <param name="HoldPercent">
/// The looser share that keeps a rhythm once found: in the half-rate cadence a unit in the repeat phase is a repeat at
/// most this percentage of the unit before, so a repeat refined a little more than usual does not break it.
/// </param>
/// <param name="LockUnits">Units in a row that must alternate small and large before the half-rate cadence is trusted.</param>
/// <param name="UnlockUnits">
/// Large units in a row, where the half-rate cadence expects repeats, before it is dropped (the game now draws every
/// picture). A single one is a slip of the rhythm (a picture the console sent late) and moves it on by one.
/// </param>
public sealed record RepeatSettings(int RepeatBytes, int RepeatPercent, int HoldPercent, int LockUnits, int UnlockUnits)
{
    /// <summary>A repeat is at most 2 KB: several times a repeat's size, a twentieth of a new picture in motion.</summary>
    public const int StandardRepeatBytes = 2048;

    /// <summary>75 %: above the measured repeats (at most 67 %); a 60 fps game alternating by this much eight units in a row is unlikely.</summary>
    public const int StandardRepeatPercent = 75;

    /// <summary>90 %: a repeat is still clearly smaller than the picture it refines.</summary>
    public const int StandardHoldPercent = 90;

    /// <summary>The whole, for <see cref="RepeatPercent"/>.</summary>
    public const int Percent = 100;

    /// <summary>Four pairs, 133 ms at 60 a second.</summary>
    public const int StandardLockUnits = 8;

    /// <summary>Three, 100 ms at 60 a second.</summary>
    public const int StandardUnlockUnits = 3;

    public static RepeatSettings Standard { get; } = new(StandardRepeatBytes, StandardRepeatPercent, StandardHoldPercent, StandardLockUnits, StandardUnlockUnits);
}
