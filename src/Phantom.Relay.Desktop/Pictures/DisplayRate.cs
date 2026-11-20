// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Pacing;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.Desktop.Pictures;

/// <summary>
/// The display's refresh and the game's rate of new pictures as the frame pacer measured them (<see cref="Observe"/>,
/// once its refresh estimate settled), kept across pacers: the target rate a multiplier gives
/// (<see cref="FramesPerSecond"/>: x4 of a 30 fps game is 120) and the largest multiplier the display can show
/// (<see cref="Limit"/>: a 60 Hz display shows x2 of a 30 fps game, not x4). Unknown at first: no limit, a nominal
/// 30 fps game. Observed on the decode thread, read on any.
/// </summary>
internal sealed class DisplayRate
{
    /// <summary>A display a hair slower than a whole multiple (59.94 Hz for 60) still shows it.</summary>
    private const double Tolerance = 0.05;

    private static readonly TimeSpan NominalSource = TimeSpan.FromSeconds(1.0 / 30);

    private long _refreshTicks;
    private long _sourceTicks = NominalSource.Ticks;

    /// <summary>The display's measured refresh; zero until a pacer settled.</summary>
    public TimeSpan Refresh => TimeSpan.FromTicks(Volatile.Read(ref _refreshTicks));

    /// <summary>The display's measured rate, in hertz; zero until a pacer settled.</summary>
    public double Hertz
    {
        get
        {
            var refresh = Refresh;

            if (refresh <= TimeSpan.Zero)
            {
                return 0;
            }

            return 1 / refresh.TotalSeconds;
        }
    }

    /// <summary>Takes the pacer's measures once its refresh estimate settled; whether they are now known.</summary>
    public bool Observe(FramePacer pacer)
    {
        ArgumentNullException.ThrowIfNull(pacer);

        if (!pacer.RefreshSettled)
        {
            return false;
        }

        Volatile.Write(ref _refreshTicks, pacer.RefreshInterval.Ticks);
        Volatile.Write(ref _sourceTicks, pacer.SourceInterval.Ticks);

        return true;
    }

    /// <summary>The frames shown a second at <paramref name="multiplier"/>: the game's new pictures times it.</summary>
    public double FramesPerSecond(int multiplier)
    {
        return multiplier / TimeSpan.FromTicks(Volatile.Read(ref _sourceTicks)).TotalSeconds;
    }

    /// <summary><paramref name="requested"/>, or fewer when the display cannot show that many a second (x2 at least).</summary>
    public int Limit(int requested)
    {
        var refresh = Refresh;

        if (refresh <= TimeSpan.Zero)
        {
            return requested;
        }

        var source = TimeSpan.FromTicks(Volatile.Read(ref _sourceTicks));
        var most = (int)Math.Floor((source.TotalSeconds / refresh.TotalSeconds) + Tolerance);

        return Math.Clamp(most, FrameGeneration.Doubled, Math.Max(FrameGeneration.Doubled, requested));
    }
}
