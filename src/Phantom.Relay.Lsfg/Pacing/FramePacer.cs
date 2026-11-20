// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Lsfg.Pacing;

/// <summary>
/// Says what each display refresh shows when real frames arrive at about the source rate and generated frames are
/// made between them. When real frame n arrives (at A), the pair n - 1 to n gets <c>multiplier</c> slots spread over
/// the pair's interval I: generated step k is due at A + (k - 1) I / M, so the first is shown as soon as it is ready,
/// and real frame n is due at A + (M - 1) I / M, so it is never held back a whole source frame. At each refresh the
/// latest due slot that is ready is shown; a generated frame that is late when a later slot is due is skipped, never
/// shown out of order. I is the real time between the pair's two frames when the caller knows it (the source's
/// timestamps: a game that dips to 25 fps spreads that pair over 50 ms), else the arrivals' smoothed interval, starting
/// at the nominal one. A slot due within half a refresh is shown at this refresh; the refresh is measured from the
/// calls to <see cref="OnRefresh"/>, starting at the one given.
/// </summary>
public sealed class FramePacer
{
    // The interval and refresh estimates move an eighth of the way to each new measure.
    private const long SmoothingDivisor = 8;

    // A gap longer than this many nominal intervals is a pause, not a rate, and does not move the estimate.
    private const long PauseIntervals = 4;

    // A pair is spread over at most this many nominal intervals, so its real frame is due before the next one is likely to come.
    private const long LongestPairIntervals = 2;

    // A slot due within half a refresh after this one is shown at this one.
    private const long HalfRefresh = 2;

    // A gap between refreshes up to one and a half times the first guess is a refresh (a missed one at a fast display
    // still counts, nudging the estimate up a little); a longer one is a stall.
    private const long RefreshGapNumerator = 3;
    private const long RefreshGapDenominator = 2;

    // Refreshes measured before the refresh estimate is taken as the display's: the first guess's weight is then (7/8)^60,
    // under a 1/1000 (thirty leave 2 %, which reads a 120 Hz display as 118 Hz). A quarter second at 240 Hz, one at 60 Hz.
    private const int SettledRefreshes = 60;

    // Before the first frame there is none: frame -1.
    private const int NoFrame = -1;

    // Slots count from 1; 0 is none of the pair's slots shown yet.
    private const int NoSlot = 0;

    private readonly TimeProvider _clock;
    private readonly int _multiplier;
    private readonly long _nominal;
    private readonly long _slowestRefresh;
    private long _refresh;
    private long _lastRefresh;
    private long _interval;
    private long _frame = NoFrame;
    private long _arrival;
    private bool _hasPrevious;
    private int _ready;
    private int _shown = NoSlot;
    private int _refreshes;

    /// <summary>
    /// A pacer for <paramref name="multiplier"/> shown frames per real frame (1 shows real frames only), real frames
    /// nominally <paramref name="sourceInterval"/> apart, on a display refreshing about every <paramref name="refreshInterval"/>
    /// (a guess it then measures; a slower display than it is not followed).
    /// </summary>
    public FramePacer(TimeProvider clock, int multiplier, TimeSpan sourceInterval, TimeSpan refreshInterval)
    {
        _clock = clock;
        _multiplier = Math.Max(1, multiplier);
        _nominal = Ticks(clock, sourceInterval);
        _refresh = Math.Max(1, Ticks(clock, refreshInterval));
        _slowestRefresh = _refresh * RefreshGapNumerator / RefreshGapDenominator;
        _interval = _nominal;
    }

    /// <summary>The interval between real frames the current pair is spread over.</summary>
    public TimeSpan SourceInterval => TimeSpan.FromSeconds(_interval / (double)_clock.TimestampFrequency);

    /// <summary>The display's refresh as measured from the calls to <see cref="OnRefresh"/>.</summary>
    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(Volatile.Read(ref _refresh) / (double)_clock.TimestampFrequency);

    /// <summary>Whether enough refreshes were measured for <see cref="RefreshInterval"/> to be the display's own; any thread.</summary>
    public bool RefreshSettled => Volatile.Read(ref _refreshes) >= SettledRefreshes;

    /// <summary>
    /// Starts the refresh estimate at <paramref name="refreshInterval"/>, a display's refresh measured before (by the
    /// pacer this one replaces), rather than at the guess it was made with; stalls are still judged by the guess.
    /// </summary>
    public void Prime(TimeSpan refreshInterval)
    {
        var ticks = Ticks(_clock, refreshInterval);

        if (ticks > 0 && ticks <= _slowestRefresh)
        {
            _refresh = ticks;
        }
    }

    /// <summary>Real frame <paramref name="frame"/> arrived now; the pair before it starts, spread over the arrivals' interval.</summary>
    [HotPath]
    public void OnRealFrame(long frame)
    {
        OnRealFrame(frame, TimeSpan.Zero);
    }

    /// <summary>
    /// Real frame <paramref name="frame"/> arrived now, <paramref name="sourceInterval"/> after the one before at the
    /// source (zero when not known: the arrivals' interval is used); the pair before it starts.
    /// </summary>
    [HotPath]
    public void OnRealFrame(long frame, TimeSpan sourceInterval)
    {
        var now = _clock.GetTimestamp();

        if (_frame != NoFrame)
        {
            Measure(now - _arrival, Ticks(_clock, sourceInterval));
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
        var now = _clock.GetTimestamp();
        MeasureRefresh(now);

        if (_frame == NoFrame)
        {
            return PacedFrame.Hold;
        }

        var due = DueSlot(now);

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

        var elapsed = now + (_refresh / HalfRefresh) - _arrival;
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

    // The pair's own interval at the source when it is known and no pause, else the arrivals' smoothed.
    private void Measure(long gap, long source)
    {
        if (source > 0 && source <= _nominal * PauseIntervals)
        {
            _interval = Math.Min(source, _nominal * LongestPairIntervals);

            return;
        }

        Smooth(gap);
    }

    private void Smooth(long gap)
    {
        if (gap <= 0 || gap > _nominal * PauseIntervals)
        {
            return;
        }

        _interval += (gap - _interval) / SmoothingDivisor;
    }

    // The time since the last refresh, unless a stall (or the first): the display's refresh, smoothed.
    private void MeasureRefresh(long now)
    {
        var gap = now - _lastRefresh;
        var first = _lastRefresh == 0;
        _lastRefresh = now;

        if (first || gap <= 0 || gap > _slowestRefresh)
        {
            return;
        }

        Volatile.Write(ref _refresh, _refresh + ((gap - _refresh) / SmoothingDivisor));

        if (_refreshes < SettledRefreshes)
        {
            Volatile.Write(ref _refreshes, _refreshes + 1);
        }
    }
}
