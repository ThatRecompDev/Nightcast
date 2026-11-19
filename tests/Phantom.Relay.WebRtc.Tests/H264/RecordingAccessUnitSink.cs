// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.H264;

namespace Phantom.Relay.WebRtc.Tests.H264;

/// <summary>Remembers the access units delivered to it as hex, with their timestamps and flags, or only counts them.</summary>
public sealed class RecordingAccessUnitSink : IAccessUnitSink
{
    private readonly bool _record;

    public RecordingAccessUnitSink(bool record)
    {
        _record = record;
    }

    public List<(string Hex, uint Timestamp, H264AccessUnitContent Content)> Units { get; } = [];

    public int Count { get; private set; }

    public void Deliver(ReadOnlySpan<byte> accessUnit, uint timestamp, H264AccessUnitContent content)
    {
        Count++;

        if (_record)
        {
            Units.Add((Convert.ToHexString(accessUnit), timestamp, content));
        }
    }
}
