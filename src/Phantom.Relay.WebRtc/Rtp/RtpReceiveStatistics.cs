// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// What a receiver reports about one source (RFC 3550 section 6.4.1 and appendices A.3 and A.8): packets expected and
/// lost since the start and since the last report, the extended highest sequence number, the interarrival jitter in
/// timestamp units, and the time since the source's last sender report. Every received packet is recorded as it
/// arrives, before any reordering. Time comes from the injected clock.
/// </summary>
public sealed class RtpReceiveStatistics
{
    /// <summary>The jitter estimate moves a sixteenth of the way to each new difference (RFC 3550 A.8).</summary>
    private const double JitterGain = 16;

    /// <summary>The fraction lost is a fixed-point number with eight bits after the point.</summary>
    private const int FractionShift = 8;

    private const int FractionWordShift = 24;
    private const int CumulativeLostMask = 0xFFFFFF;
    private const int MaxCumulativeLost = 0x7FFFFF;
    private const int MinCumulativeLost = -0x800000;

    /// <summary>The DLSR is in units of 1/65536 of a second; the LSR is the middle 32 bits of the NTP time.</summary>
    private const long DelayUnitsPerSecond = 65536;

    private const int NtpMiddleShift = 16;

    private const int LostOffset = 4;
    private const int HighestOffset = 8;
    private const int JitterOffset = 12;
    private const int LastReportOffset = 16;
    private const int DelayOffset = 20;

    private readonly TimeProvider _clock;
    private readonly double _clockRate;
    private readonly RtpSequenceUnwrapper _sequences = new();
    private bool _started;
    private long _base;
    private long _received;
    private long _expectedPrior;
    private long _receivedPrior;
    private uint _lastTransit;
    private bool _hasTransit;
    private double _jitter;
    private uint _lastSenderReport;
    private long _lastSenderReportAt;

    public RtpReceiveStatistics(TimeProvider clock, int clockRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(clockRate, 1);

        _clock = clock;
        _clockRate = clockRate;
    }

    public uint Ssrc { get; private set; }

    public long Received => _received;

    /// <summary>Packets expected from the first sequence number to the highest, minus those received (may go negative with duplicates).</summary>
    public long CumulativeLost => Expected() - _received;

    /// <summary>The interarrival jitter, in RTP timestamp units.</summary>
    public double Jitter => _jitter;

    public long HighestSequence => _sequences.Highest;

    /// <summary>Records one received packet of <paramref name="ssrc"/>.</summary>
    [HotPath]
    public void Record(uint ssrc, ushort sequence, uint timestamp)
    {
        long extended = _sequences.Unwrap(sequence);

        if (!_started)
        {
            _started = true;
            _base = extended;
            Ssrc = ssrc;
        }

        _received++;

        // The transit time in timestamp units; its change from packet to packet feeds the jitter (wrap-safe in 32 bits).
        uint arrival = (uint)(long)(_clock.GetTimestamp() * _clockRate / _clock.TimestampFrequency);
        uint transit = arrival - timestamp;

        if (_hasTransit)
        {
            long difference = Math.Abs((long)(int)(transit - _lastTransit));
            _jitter += (difference - _jitter) / JitterGain;
        }

        _lastTransit = transit;
        _hasTransit = true;
    }

    /// <summary>Records the source's sender report, whose NTP time the next report block echoes.</summary>
    [HotPath]
    public void RecordSenderReport(ulong ntpTimestamp)
    {
        _lastSenderReport = (uint)(ntpTimestamp >> NtpMiddleShift);
        _lastSenderReportAt = _clock.GetTimestamp();
    }

    /// <summary>Writes this source's 24-octet report block, and starts the next report's interval.</summary>
    [HotPath]
    public void WriteReportBlock(Span<byte> destination)
    {
        long expected = Expected();
        long expectedInterval = expected - _expectedPrior;
        long lostInterval = expectedInterval - (_received - _receivedPrior);
        _expectedPrior = expected;
        _receivedPrior = _received;

        long fraction = 0;

        if (expectedInterval > 0 && lostInterval > 0)
        {
            fraction = (lostInterval << FractionShift) / expectedInterval;
        }

        long lost = Math.Clamp(expected - _received, MinCumulativeLost, MaxCumulativeLost);
        BinaryPrimitives.WriteUInt32BigEndian(destination, Ssrc);
        BinaryPrimitives.WriteUInt32BigEndian(destination[LostOffset..], (uint)((fraction << FractionWordShift) | (lost & CumulativeLostMask)));
        BinaryPrimitives.WriteUInt32BigEndian(destination[HighestOffset..], (uint)_sequences.Highest);
        BinaryPrimitives.WriteUInt32BigEndian(destination[JitterOffset..], (uint)_jitter);
        BinaryPrimitives.WriteUInt32BigEndian(destination[LastReportOffset..], _lastSenderReport);

        BinaryPrimitives.WriteUInt32BigEndian(destination[DelayOffset..], DelaySinceLastSenderReport());
    }

    private long Expected()
    {
        if (!_started)
        {
            return 0;
        }

        return _sequences.Highest - _base + 1;
    }

    private uint DelaySinceLastSenderReport()
    {
        if (_lastSenderReport == 0)
        {
            return 0;
        }

        long elapsed = _clock.GetTimestamp() - _lastSenderReportAt;

        return (uint)(elapsed * DelayUnitsPerSecond / _clock.TimestampFrequency);
    }
}
