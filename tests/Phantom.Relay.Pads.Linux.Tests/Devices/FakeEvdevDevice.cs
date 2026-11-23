// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Devices;
using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Tests.Devices;

/// <summary>A pad without hardware: the event batches a test queues, the rumble it was asked for, never a wait.</summary>
internal sealed class FakeEvdevDevice : IEvdevDevice
{
    private readonly Queue<byte[]> _batches = new();

    /// <summary>Set to make every read from now on find the pad gone.</summary>
    public bool Unplugged { get; set; }

    public bool Disposed { get; private set; }

    public int Wakes { get; private set; }

    public int Resynchronisations { get; private set; }

    /// <summary>The events a resynchronisation feeds the pad (the device's state now).</summary>
    public List<InputEvent> Current { get; } = [];

    public List<(ushort Strong, ushort Weak)> Rumbles { get; } = [];

    /// <summary>Queues one read's worth of events.</summary>
    public void Queue(params InputEvent[] events)
    {
        var bytes = new byte[events.Length * InputEvent.Size];

        for (var index = 0; index < events.Length; index++)
        {
            events[index].Write(bytes.AsSpan(index * InputEvent.Size));
        }

        _batches.Enqueue(bytes);
    }

    public EvdevReadResult Read(Span<byte> events, out int length)
    {
        length = 0;

        if (Unplugged)
        {
            return EvdevReadResult.Gone;
        }

        if (!_batches.TryDequeue(out var next))
        {
            return EvdevReadResult.Idle;
        }

        length = Math.Min(next.Length, events.Length);
        next.AsSpan(0, length).CopyTo(events);

        return EvdevReadResult.Events;
    }

    public void Wake()
    {
        Wakes++;
    }

    public void Resynchronise(EvdevPadState pad)
    {
        Resynchronisations++;

        foreach (var input in Current)
        {
            pad.Apply(input);
        }
    }

    public bool Rumble(ushort strong, ushort weak)
    {
        Rumbles.Add((strong, weak));

        return true;
    }

    public void Dispose()
    {
        Disposed = true;
    }
}
