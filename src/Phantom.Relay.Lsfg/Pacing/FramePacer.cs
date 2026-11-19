// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Lsfg.Pacing;

/// <summary>
/// Says what each display refresh shows when real frames arrive at about the source rate and generated frames are
/// made between them. When real frame n arrives (at A), the pair n - 1 to n gets <c>multiplier</c> slots spread over
/// one source interval I: generated step k is due at A + (k - 1) I / M, so the first is shown as soon as it is ready,
/// and real frame n is due at A + (M - 1) I / M, so it is never held back a whole source frame. At each refresh the
/// latest due slot that is ready is shown; a generated frame that is late when a later slot is due is skipped, never
/// shown out of order. I is the arrivals' smoothed interval, starting at the nominal one.
/// </summary>
public sealed class FramePacer
{
    // The interval estimate moves an eighth of the way to each new arrival gap.
    private const long SmoothingDivisor = 8;

    // A gap longer than this many nominal intervals is a pause, not a rate, and does not move the estimate.
    private const long PauseIntervals = 4;

    // A slot due within half a refresh after this one is shown at this one.
    private const long HalfRefresh = 2;

    // Before the first frame there is none: frame -1.
    private const int NoFrame = -1;

    // Slots count from 1; 0 is none of the pair's slots shown yet.
    private const int NoSlot = 0;

    private readonly TimeProvider _clock;
    private readonly int _multiplier;
    private readonly long _nominal;
    private readonly long _tolerance;
    private long _interval;
    private long _frame = NoFrame;
    private long _arrival;
    private bool _hasPrevious;
    private int _ready;
    private int _shown = NoSlot;

    /// <summary>
    /// A pacer for <paramref name="multiplier"/> shown frames per real frame (1 shows real frames only), real frames
    /// nominally <paramref name="sourceInterval"/> apart, on a display refreshing every <paramref name="refreshInterval"/>.
    /// </summary>
    public FramePacer(TimeProvider clock, int multiplier, TimeSpan sourceInterval, TimeSpan refreshInterval)
    {
        _clock = clock;
        _multiplier = Math.Max(1, multiplier);
        _nominal = Ticks(clock, sourceInterval);
        _tolerance = Ticks(clock, refreshInterval) / HalfRefresh;
        _interval = _nominal;
    }

    /// <summary>The smoothed interval between real frames.</summary>
    public TimeSpan SourceInterval => TimeSpan.FromSeconds(_interval / (double)_clock.TimestampFrequency);

    /// <summary>Real frame <paramref name="frame"/> arrived now; the pair before it starts.</summary>
    [HotPath]
    public void OnRealFrame(long frame)
    {
        var now = _clock.GetTimestamp();

        if (_frame != NoFrame)
        {
            Smooth(now - _arrival);
        }

        _hasPrevious = _frame != NoFrame;
        _frame = frame;
        _arrival = now;
        _ready = 0;
        _shown = NoSlot;
    }

    /// <summary>Generated step <paramref name="step"/> (1 to multiplier - 1) between frame - 1 and <paramref name="frame"/> is ready.</summary>
    [HotPath]
    public void OnGeneratedReady(long frame, int step)
    {
        if (frame == _frame && step >= 1 && step < _multiplier)
        {
            _ready |= 1 << step;
        }
    }

    /// <summary>What this refresh shows: the latest due slot that is ready and newer than what is on screen, else hold.</summary>
    [HotPath]
    public PacedFrame OnRefresh()
    {
        if (_frame == NoFrame)
        {
            return PacedFrame.Hold;
        }

        var due = DueSlot(_clock.GetTimestamp());

        for (var slot = due; slot > _shown; slot--)
        {
            if (IsReady(slot))
            {
                _shown = slot;

                return Show(slot);
            }
        }

        return PacedFrame.Hold;
    }

    private static long Ticks(TimeProvider clock, TimeSpan span)
    {
        return (long)(span.TotalSeconds * clock.TimestampFrequency);
    }

    // Slots 1 .. M - 1 are the generated steps, slot M the real frame; the first frame has no pair, only its real slot.
    private int DueSlot(long now)
    {
        if (!_hasPrevious)
        {
            return _multiplier;
        }

        var elapsed = now + _tolerance - _arrival;
        var spacing = Math.Max(1, _interval / _multiplier);
        var slot = (int)Math.Min(_multiplier, (elapsed / spacing) + 1);

        return slot;
    }

    private bool IsReady(int slot)
    {
        return slot == _multiplier || (_ready & (1 << slot)) != 0;
    }

    [HotPath]
    private PacedFrame Show(int slot)
    {
        if (slot == _multiplier)
        {
            return new PacedFrame(PacedFrameKind.Real, _frame, 0, _multiplier);
        }

        return new PacedFrame(PacedFrameKind.Generated, _frame, slot, _multiplier);
    }

    private void Smooth(long gap)
    {
        if (gap <= 0 || gap > _nominal * PauseIntervals)
        {
            return;
        }

        _interval += (gap - _interval) / SmoothingDivisor;
    }
}
