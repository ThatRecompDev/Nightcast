// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Hashing;
using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Stun;

/// <summary>
/// Writes one STUN message into a caller's buffer: the header, then attributes in order, then optionally
/// MESSAGE-INTEGRITY and FINGERPRINT, which must come last and in that order. Allocation-free.
/// </summary>
public ref struct StunWriter
{
    private const int LengthOffset = 2;
    private const int CookieOffset = 4;
    private const int TransactionOffset = 8;
    private const int PortXorShift = 16;

    private readonly Span<byte> _buffer;
    private int _length;

    [HotPath]
    public StunWriter(Span<byte> buffer, ushort type, ReadOnlySpan<byte> transactionId)
    {
        _buffer = buffer;
        BinaryPrimitives.WriteUInt16BigEndian(buffer, type);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[CookieOffset..], StunFormat.MagicCookie);
        transactionId.CopyTo(buffer[TransactionOffset..]);
        _length = StunFormat.HeaderLength;
        WriteLength();
    }

    /// <summary>The bytes written so far.</summary>
    public readonly int Length => _length;

    public readonly ReadOnlySpan<byte> Written => _buffer[.._length];

    /// <summary>An attribute with a raw value, padded with <paramref name="padding"/> to a multiple of four bytes.</summary>
    [HotPath]
    public void Add(ushort type, ReadOnlySpan<byte> value, byte padding)
    {
        var room = BeginAttribute(type, value.Length);
        value.CopyTo(room);
        room[value.Length..Padded(value.Length)].Fill(padding);
        EndAttribute(value.Length);
    }

    [HotPath]
    public void Add(ushort type, ReadOnlySpan<byte> value)
    {
        Add(type, value, 0);
    }

    [HotPath]
    public void AddUInt32(ushort type, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(BeginAttribute(type, sizeof(uint)), value);
        EndAttribute(sizeof(uint));
    }

    [HotPath]
    public void AddUInt64(ushort type, ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(BeginAttribute(type, sizeof(ulong)), value);
        EndAttribute(sizeof(ulong));
    }

    /// <summary>An attribute with no value (USE-CANDIDATE).</summary>
    [HotPath]
    public void AddFlag(ushort type)
    {
        BeginAttribute(type, 0);
        EndAttribute(0);
    }

    /// <summary>XOR-MAPPED-ADDRESS for an IPv4 endpoint: port and address exclusive-or the magic cookie.</summary>
    [HotPath]
    public void AddXorMappedAddress(DatagramEndpoint endpoint)
    {
        AddXorAddress(StunFormat.XorMappedAddress, endpoint);
    }

    /// <summary>Any XOR address attribute of an IPv4 endpoint (XOR-MAPPED, and TURN's XOR-PEER and XOR-RELAYED).</summary>
    [HotPath]
    public void AddXorAddress(ushort type, DatagramEndpoint endpoint)
    {
        var room = BeginAttribute(type, StunFormat.IPv4AddressValueLength);
        room[0] = 0;
        room[1] = StunFormat.FamilyIPv4;

        // The port and the address, each exclusive-or the magic cookie.
        ushort port = (ushort)(endpoint.Port ^ (StunFormat.MagicCookie >> PortXorShift));
        BinaryPrimitives.WriteUInt16BigEndian(room[sizeof(ushort)..], port);
        BinaryPrimitives.WriteUInt32BigEndian(room[sizeof(uint)..], endpoint.Address ^ StunFormat.MagicCookie);
        EndAttribute(StunFormat.IPv4AddressValueLength);
    }

    /// <summary>MESSAGE-INTEGRITY over everything written so far.</summary>
    [HotPath]
    public void AddIntegrity(StunIntegrity integrity)
    {
        int start = _length;
        ushort lengthThroughIntegrity = (ushort)(start + StunFormat.AttributeHeaderLength + StunFormat.IntegrityLength - StunFormat.HeaderLength);
        var room = BeginAttribute(StunFormat.MessageIntegrity, StunFormat.IntegrityLength);
        integrity.Compute(_buffer[..start], lengthThroughIntegrity, room);
        EndAttribute(StunFormat.IntegrityLength);
    }

    /// <summary>FINGERPRINT: the CRC-32 of everything written so far, exclusive-or "STUN". Always the last attribute.</summary>
    [HotPath]
    public void AddFingerprint()
    {
        int start = _length;
        var room = BeginAttribute(StunFormat.Fingerprint, StunFormat.FingerprintLength);
        uint crc = Crc32.Hash(_buffer[..start]) ^ StunFormat.FingerprintXor;
        BinaryPrimitives.WriteUInt32BigEndian(room, crc);
        EndAttribute(StunFormat.FingerprintLength);
    }

    private static int Padded(int length)
    {
        return (length + StunFormat.Alignment - 1) & ~(StunFormat.Alignment - 1);
    }

    /// <summary>
    /// Writes the attribute header and grows the header's length to cover the attribute, as integrity and fingerprint
    /// require before they are computed; returns the room for the padded value.
    /// </summary>
    private Span<byte> BeginAttribute(ushort type, int valueLength)
    {
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[_length..], type);
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[(_length + sizeof(ushort))..], (ushort)valueLength);
        int valueStart = _length + StunFormat.AttributeHeaderLength;
        int end = valueStart + Padded(valueLength);
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[LengthOffset..], (ushort)(end - StunFormat.HeaderLength));

        return _buffer[valueStart..end];
    }

    private void EndAttribute(int valueLength)
    {
        _length += StunFormat.AttributeHeaderLength + Padded(valueLength);
    }

    private readonly void WriteLength()
    {
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[LengthOffset..], (ushort)(_length - StunFormat.HeaderLength));
    }
}
