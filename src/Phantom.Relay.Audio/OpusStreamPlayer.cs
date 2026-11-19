// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Audio.Output;
using Phantom.Engine.Core;
using Phantom.Relay.Audio.Playout;

namespace Phantom.Relay.Audio;

/// <summary>
/// Plays the console's game sound with the least delay that does not crackle. The network thread hands it each Opus
/// payload (<see cref="Push"/>); the audio device pulls it as an <see cref="IAudioRenderer"/> on its own clock. In
/// between sits an adaptive jitter buffer: packets are put back in sequence order, a lost one is rebuilt from the
/// next one's in-band FEC or concealed by the decoder, a late one is dropped, the delay follows the measured arrival
/// jitter (up at once, down slowly) and a resampling ratio within 0.5 % holds it against the drift between the
/// console's clock and the sound card's. Packets cross threads through preallocated slots and lock-free rings;
/// neither side allocates or locks.
/// </summary>
public sealed class OpusStreamPlayer : IAudioRenderer
{
    /// <summary>
    /// The device buffer to ask for: 512 frames, 10.7 ms at 48 kHz. sokol_audio cuts its buffer into 128-frame packets
    /// and refuses one that is not a multiple of them (BACKEND_BUFFER_SIZE_ISNT_MULTIPLE_OF_PACKET_SIZE), so 480, an
    /// exact 10 ms, opened no device on the owner's PC (2026-11-19). A device that underruns with it wants more.
    /// </summary>
    public const int DeviceBufferFrames = 512;

    /// <summary>Packets in flight and waiting together: the reorder window and as much again crossing threads.</summary>
    private const int PacketSlots = 128;

    private readonly IOpusFrameDecoder _decoder;
    private readonly TimeProvider _clock;
    private readonly PacketQueue _queue = new(PacketSlots);
    private readonly PlayoutBuffer _playout;
    private long _refused;

    /// <param name="decoder">The decoder; the player is its only user.</param>
    /// <param name="clock">Arrival and render times.</param>
    /// <param name="outputRate">The device's rate (<c>IAudioDevice.Format.SampleRate</c>); 48 kHz needs no conversion.</param>
    /// <param name="settings">The delay's bounds and start (<see cref="PlayoutSettings.Default"/>).</param>
    public OpusStreamPlayer(IOpusFrameDecoder decoder, TimeProvider clock, int outputRate, PlayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputRate);

        _decoder = decoder;
        _clock = clock;
        _playout = new PlayoutBuffer(decoder, _queue, settings, outputRate, clock.TimestampFrequency);
        SampleRate = outputRate;
    }

    /// <summary>What to open the device with: 48 kHz stereo, 512-frame (10.7 ms) buffers.</summary>
    public static AudioDeviceRequest DeviceRequest { get; } =
        new(OpusStreamFormat.SampleRate, OpusStreamFormat.Channels, DeviceBufferFrames);

    public int SampleRate { get; }

    public int Channels => OpusStreamFormat.Channels;

    /// <summary>A snapshot of the delay, the drift ratio and what went wrong, from any thread.</summary>
    public OpusStreamStats Stats => new(
        _playout.DelaySamples / OpusStreamFormat.SamplesPerMillisecond,
        _playout.TargetSamples / OpusStreamFormat.SamplesPerMillisecond,
        _playout.Underruns,
        _playout.Concealed,
        _playout.FecRecoveries,
        _playout.LateDrops,
        _playout.Ratio,
        _playout.Played,
        _playout.Skipped,
        _playout.Resyncs,
        Volatile.Read(ref _refused),
        _playout.Playing);

    /// <summary>
    /// Network thread: one decrypted RTP payload of the Opus stream with its sequence number and timestamp. It is
    /// copied; the caller keeps its buffer. A malformed or oversized packet, or one with no free slot, is refused,
    /// counted, and later concealed as lost.
    /// </summary>
    [HotPath]
    public void Push(ReadOnlySpan<byte> payload, ushort sequence, uint timestamp)
    {
        var arrival = _clock.GetTimestamp();
        var samples = 0;

        if (!payload.IsEmpty && payload.Length <= OpusStreamFormat.MaxPayloadBytes)
        {
            samples = _decoder.PacketSamples(payload);
        }

        if (samples <= 0 || samples > OpusStreamFormat.MaxPacketSamples || !_queue.TryWrite(payload, sequence, timestamp, samples, arrival))
        {
            Volatile.Write(ref _refused, _refused + 1);
        }
    }

    /// <summary>Audio thread: the device's pull.</summary>
    [HotPath]
    public void Render(Span<float> interleaved, int frames)
    {
        _playout.Render(interleaved, frames, _clock.GetTimestamp());
    }
}
