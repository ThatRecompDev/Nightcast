// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// A bounded queue between a thread that must never wait (the decode thread, the render thread) and a recorder's
/// writer thread: <see cref="TryAdd"/> never blocks and, when the queue is full, drops the item and counts it
/// (<see cref="Dropped"/>); <see cref="TryTake"/> waits for an item at most a given time. After <see cref="Complete"/>
/// nothing more is taken in, and the writer drains what is left. Its slots are made once; nothing is allocated after.
/// </summary>
internal sealed class RecordingQueue<T>
    where T : struct
{
    private readonly object _gate = new();
    private readonly T[] _items;
    private int _head;
    private int _count;
    private long _dropped;
    private bool _completed;

    public RecordingQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _items = new T[capacity];
    }

    public int Capacity => _items.Length;

    /// <summary>Items refused because the queue was full.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Whether no more is taken in.</summary>
    public bool IsCompleted
    {
        get
        {
            lock (_gate)
            {
                return _completed;
            }
        }
    }

    /// <summary>Adds <paramref name="item"/> unless the queue is full (counted) or completed; never waits.</summary>
    [HotPath]
    public bool TryAdd(in T item)
    {
        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            if (_count == _items.Length)
            {
                _dropped++;

                return false;
            }

            _items[(_head + _count) % _items.Length] = item;
            _count++;
            Monitor.Pulse(_gate);
        }

        return true;
    }

    /// <summary>The oldest item, waiting up to <paramref name="wait"/> for one; false when none came (or the queue is completed and empty).</summary>
    public bool TryTake(out T item, TimeSpan wait)
    {
        lock (_gate)
        {
            if (_count == 0 && !_completed)
            {
                _ = Monitor.Wait(_gate, wait);
            }

            if (_count == 0)
            {
                item = default;

                return false;
            }

            item = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            _count--;
        }

        return true;
    }

    /// <summary>Takes nothing more in and wakes the writer, which drains what is left.</summary>
    public void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            Monitor.PulseAll(_gate);
        }
    }
}
