// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;

namespace Phantom.Relay.Audio.Tests.Fakes;

/// <summary>
/// A console and a sound card on one manual clock: packet k of 10 ms is sent when the schedule says (or never), the
/// device pulls 10 ms every 10 ms, and both go through the player in time order, sends first on a tie. The first
/// sequence number sits just below the wrap, so every run crosses it.
/// </summary>
public sealed class StreamSimulation
{
    public const int PacketSamples = 480;

    public const double PacketMilliseconds = 10;

    public const int RenderFrames = 480;

    public const double RenderMilliseconds = 10;

    public const ushort FirstSequence = 65500;

    private readonly ManualTimeProvider _clock = new(DateTimeOffset.UnixEpoch);
    private readonly byte[] _payload = new byte[FakeOpusDecoder.PayloadLength];
    private readonly List<(double Time, int Index)> _sends;
    private readonly Func<int, bool> _withFec;
    private long _nowTicks;
    private int _nextSend;
    private int _renders;

    /// <param name="horizonMilliseconds">The longest the simulation will run.</param>
    /// <param name="sendTime">When packet k is sent, in milliseconds; NaN for never.</param>
    /// <param name="withFec">Whether packet k carries FEC for the one before it.</param>
    public StreamSimulation(double horizonMilliseconds, Func<int, double> sendTime, Func<int, bool> withFec)
    {
        Decoder = new FakeOpusDecoder(PacketSamples);
        Player = new OpusStreamPlayer(Decoder, _clock, OpusStreamFormat.SampleRate, PlayoutSettings.Default);
        _withFec = withFec;

        // Twice the packets the horizon holds, for a console that sends faster than the device plays.
        var packets = (int)(horizonMilliseconds / PacketMilliseconds * 2);
        _sends = Enumerable.Range(0, packets)
            .Select(index => (Time: sendTime(index), Index: index))
            .Where(send => !double.IsNaN(send.Time) && send.Time <= horizonMilliseconds)
            .OrderBy(send => send.Time)
            .ThenBy(send => send.Index)
            .ToList();
    }

    public FakeOpusDecoder Decoder { get; }

    public OpusStreamPlayer Player { get; }

    public float[] Output { get; } = new float[RenderFrames * OpusStreamFormat.Channels];

    public double NowMilliseconds => _nowTicks / (double)TimeSpan.TicksPerMillisecond;

    /// <summary>A console that sends every packet on time.</summary>
    public static StreamSimulation Steady(double horizonMilliseconds)
    {
        return new StreamSimulation(horizonMilliseconds, index => index * PacketMilliseconds, _ => false);
    }

    /// <summary>Runs every send and render up to <paramref name="milliseconds"/>.</summary>
    public void RunUntil(double milliseconds)
    {
        while (_renders * RenderMilliseconds <= milliseconds)
        {
            var renderAt = _renders * RenderMilliseconds;

            while (_nextSend < _sends.Count && _sends[_nextSend].Time <= renderAt)
            {
                var send = _sends[_nextSend];
                AdvanceTo(send.Time);
                Send(send.Index, _withFec(send.Index));
                _nextSend++;
            }

            AdvanceTo(renderAt);
            Player.Render(Output, RenderFrames);
            _renders++;
        }
    }

    /// <summary>Moves the clock on to <paramref name="milliseconds"/>, never back.</summary>
    public void AdvanceTo(double milliseconds)
    {
        var ticks = (long)Math.Round(milliseconds * TimeSpan.TicksPerMillisecond);

        if (ticks > _nowTicks)
        {
            _clock.Advance(TimeSpan.FromTicks(ticks - _nowTicks));
            _nowTicks = ticks;
        }
    }

    /// <summary>Hands packet <paramref name="index"/> to the player now.</summary>
    public void Send(int index, bool withFec)
    {
        FakeOpusDecoder.Write(_payload, index, withFec);
        Player.Push(_payload, (ushort)(FirstSequence + index), (uint)(index * PacketSamples));
    }

    /// <summary>One device pull now.</summary>
    public void Render()
    {
        Player.Render(Output, RenderFrames);
    }
}
