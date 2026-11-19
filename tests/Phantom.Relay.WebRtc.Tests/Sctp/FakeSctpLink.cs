// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

/// <summary>
/// One direction of an in-memory link between two associations, standing in for DTLS: it can drop, duplicate and
/// reorder packets from a seed, or be blocked outright. Packets wait in a preallocated pool until
/// <see cref="DeliverTo"/>, so a perfect link allocates nothing once warm.
/// </summary>
public sealed class FakeSctpLink : ISctpOutput
{
    private const int PoolSize = 1024;
    private const int MaxPacketLength = 2048;

    private readonly byte[][] _pool = new byte[PoolSize][];
    private readonly int[] _lengths = new int[PoolSize];
    private readonly Stack<int> _free = new(PoolSize);
    private readonly List<int> _queue = new(PoolSize);
    private readonly SeededRandomSource _random;

    public FakeSctpLink(int seed)
    {
        _random = new SeededRandomSource((ulong)seed);

        for (int index = PoolSize - 1; index >= 0; index--)
        {
            _pool[index] = new byte[MaxPacketLength];
            _free.Push(index);
        }
    }

    public double DropRate { get; set; }

    public double DuplicateRate { get; set; }

    public double ReorderRate { get; set; }

    /// <summary>Drops every packet while true.</summary>
    public bool Blocked { get; set; }

    /// <summary>Packets handed to the link, dropped or not.</summary>
    public int Sent { get; private set; }

    /// <summary>When true, every packet sent is also kept, whole, in <see cref="Recorded"/>.</summary>
    public bool Record { get; set; }

    public List<byte[]> Recorded { get; } = [];

    public void Send(ReadOnlySpan<byte> packet)
    {
        Sent++;

        if (Record)
        {
            Recorded.Add(packet.ToArray());
        }

        if (Blocked || Chance(DropRate))
        {
            return;
        }

        Enqueue(packet);

        if (Chance(DuplicateRate))
        {
            Enqueue(packet);
        }

        if (_queue.Count >= 2 && Chance(ReorderRate))
        {
            (_queue[^1], _queue[^2]) = (_queue[^2], _queue[^1]);
        }
    }

    /// <summary>Hands every waiting packet to <paramref name="receiver"/>, oldest first.</summary>
    public void DeliverTo(SctpAssociation receiver)
    {
        int count = _queue.Count;

        for (int index = 0; index < count; index++)
        {
            int slot = _queue[index];
            receiver.Receive(_pool[slot].AsSpan(0, _lengths[slot]));
            _free.Push(slot);
        }

        _queue.RemoveRange(0, count);
    }

    private void Enqueue(ReadOnlySpan<byte> packet)
    {
        if (_free.Count == 0)
        {
            return;
        }

        int slot = _free.Pop();
        packet.CopyTo(_pool[slot]);
        _lengths[slot] = packet.Length;
        _queue.Add(slot);
    }

    private bool Chance(double rate)
    {
        return rate > 0 && _random.NextUInt32() < rate * uint.MaxValue;
    }
}
