// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;

namespace Phantom.Relay.Audio.Tests.Fakes;

/// <summary>
/// A decoder of made-up packets, since the engine has no Opus encoder: a packet is its index (two bytes, little
/// endian) and a flags byte saying whether it carries FEC for the packet before it. Every packet holds the same
/// number of samples, decodes to a level its index names, and each call is logged in a buffer made up front, so the
/// player's allocation can be measured through it.
/// </summary>
public sealed class FakeOpusDecoder : IOpusFrameDecoder
{
    public const int PayloadLength = 3;

    public const byte FecFlag = 1;

    private const int FlagsByte = 2;

    private const int LogCapacity = 1 << 16;

    private readonly FakeDecode[] _log = new FakeDecode[LogCapacity];
    private readonly int _packetSamples;
    private int _logged;

    public FakeOpusDecoder(int packetSamples)
    {
        _packetSamples = packetSamples;
    }

    /// <summary>The calls so far, in order.</summary>
    public IReadOnlyList<FakeDecode> Calls => _log[.._logged];

    /// <summary>The level packet <paramref name="index"/> decodes to, on both channels.</summary>
    public static float Level(int index)
    {
        return ((index % 1000) + 1) / 1000f;
    }

    public static void Write(Span<byte> payload, int index, bool withFec)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)index);
        payload[FlagsByte] = 0;

        if (withFec)
        {
            payload[FlagsByte] = FecFlag;
        }
    }

    /// <summary>The packet indexes of the calls of one kind, in order.</summary>
    public IReadOnlyList<int> Indexes(FakeDecodeKind kind)
    {
        return Calls.Where(call => call.Kind == kind).Select(call => call.Index).ToList();
    }

    public int PacketSamples(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != PayloadLength)
        {
            return 0;
        }

        return _packetSamples;
    }

    public int Decode(ReadOnlySpan<byte> packet, Span<float> output)
    {
        var index = BinaryPrimitives.ReadUInt16LittleEndian(packet);
        Fill(output, _packetSamples, Level(index));
        Record(FakeDecodeKind.Packet, index);

        return _packetSamples;
    }

    public int DecodeFec(ReadOnlySpan<byte> nextPacket, int samples, Span<float> output)
    {
        if ((nextPacket[FlagsByte] & FecFlag) == 0)
        {
            return 0;
        }

        var index = BinaryPrimitives.ReadUInt16LittleEndian(nextPacket) - 1;
        Fill(output, samples, Level(index));
        Record(FakeDecodeKind.Fec, index);

        return samples;
    }

    public int DecodeLost(int samples, Span<float> output)
    {
        Fill(output, samples, 0f);
        Record(FakeDecodeKind.Lost, -1);

        return samples;
    }

    private static void Fill(Span<float> output, int samples, float level)
    {
        output[..(samples * OpusStreamFormat.Channels)].Fill(level);
    }

    private void Record(FakeDecodeKind kind, int index)
    {
        if (_logged < LogCapacity)
        {
            _log[_logged] = new FakeDecode(kind, index);
            _logged++;
        }
    }
}
