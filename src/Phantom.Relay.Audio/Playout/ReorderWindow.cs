// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Audio.Playout;

/// <summary>
/// The packets waiting to play, by RTP sequence number, in a fixed window of positions from the next one due: a
/// packet goes to its sequence number's position whatever order it came in. Audio thread only.
/// </summary>
internal sealed class ReorderWindow
{
    /// <summary>The packets ahead of the next one due that it holds: 640 ms of 10 ms packets.</summary>
    public const int Capacity = 64;

    /// <summary>No packet at a position.</summary>
    public const int None = -1;

    private const int Mask = Capacity - 1;

    private readonly int[] _slots = new int[Capacity];
    private readonly ushort[] _sequences = new ushort[Capacity];

    public ReorderWindow()
    {
        Array.Fill(_slots, None);
    }

    /// <summary>The packets waiting.</summary>
    public int Count { get; private set; }

    /// <summary>Holds <paramref name="slot"/> at its sequence number's position; false when that packet is already in.</summary>
    [HotPath]
    public bool TryPlace(ushort sequence, int slot)
    {
        var at = sequence & Mask;

        if (_slots[at] != None)
        {
            return false;
        }

        _slots[at] = slot;
        _sequences[at] = sequence;
        Count++;

        return true;
    }

    /// <summary>The slot of <paramref name="sequence"/>, or <see cref="None"/>, left in place.</summary>
    [HotPath]
    public int Find(ushort sequence)
    {
        var at = sequence & Mask;

        if (_slots[at] == None || _sequences[at] != sequence)
        {
            return None;
        }

        return _slots[at];
    }

    /// <summary>Takes the slot of <paramref name="sequence"/> out, or returns <see cref="None"/>.</summary>
    [HotPath]
    public int Claim(ushort sequence)
    {
        var slot = Find(sequence);

        if (slot != None)
        {
            _slots[sequence & Mask] = None;
            Count--;
        }

        return slot;
    }

    /// <summary>Takes any slot out, to empty the window; <see cref="None"/> when it is empty.</summary>
    [HotPath]
    public int TakeAny()
    {
        for (var at = 0; at < Capacity; at++)
        {
            var slot = _slots[at];

            if (slot != None)
            {
                _slots[at] = None;
                Count--;

                return slot;
            }
        }

        return None;
    }

    /// <summary>The earliest sequence number waiting, counted from <paramref name="from"/> across the wrap.</summary>
    [HotPath]
    public ushort Lowest(ushort from)
    {
        var lowest = from;
        var nearest = int.MaxValue;

        for (var at = 0; at < Capacity; at++)
        {
            if (_slots[at] == None)
            {
                continue;
            }

            var distance = (short)(_sequences[at] - from);

            if (distance < nearest)
            {
                nearest = distance;
                lowest = _sequences[at];
            }
        }

        return lowest;
    }
}
