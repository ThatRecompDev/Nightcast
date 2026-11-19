// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Stream.Input;

/// <summary>
/// The console's newest vibration per pad, from the stream's network thread to the thread that drives the pads. A pad
/// keeps only its newest report not yet taken: the motors follow the console's latest wish, never a backlog. Safe from
/// any thread; nothing allocates.
/// </summary>
public sealed class RumbleMailbox
{
    private readonly Lock _gate = new();
    private readonly RumbleReport[] _reports = new RumbleReport[PadMailbox.MaxPads];
    private readonly bool[] _pending = new bool[PadMailbox.MaxPads];
    private long _received;

    /// <summary>Vibration reports the console sent, for the stats.</summary>
    public long Received => Interlocked.Read(ref _received);

    /// <summary>The pad's newest vibration, replacing one not taken yet; a report for a pad past the fourth is dropped.</summary>
    [HotPath]
    public void Post(in RumbleReport report)
    {
        Interlocked.Increment(ref _received);

        if (report.PadIndex >= PadMailbox.MaxPads)
        {
            return;
        }

        lock (_gate)
        {
            _reports[report.PadIndex] = report;
            _pending[report.PadIndex] = true;
        }
    }

    /// <summary>The next pad's vibration not yet taken, lowest index first; false when none waits.</summary>
    [HotPath]
    public bool TryTake(out RumbleReport report)
    {
        lock (_gate)
        {
            for (int index = 0; index < PadMailbox.MaxPads; index++)
            {
                if (!_pending[index])
                {
                    continue;
                }

                _pending[index] = false;
                report = _reports[index];

                return true;
            }
        }

        report = default;

        return false;
    }
}
