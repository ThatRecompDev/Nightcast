// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Tests.Rtp;

/// <summary>Remembers the sequence numbers delivered to it, or only counts them when made to count.</summary>
public sealed class RecordingPacketSink : IRtpPacketSink
{
    private readonly bool _record;

    public RecordingPacketSink(bool record)
    {
        _record = record;
    }

    public List<long> Sequences { get; } = [];

    public int Count { get; private set; }

    public void Deliver(ReadOnlySpan<byte> packet, long sequence)
    {
        Count++;

        if (_record)
        {
            Sequences.Add(sequence);
        }
    }
}
