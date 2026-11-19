// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// Reads an H.264 NAL unit's payload bit by bit, most significant first, skipping its emulation prevention bytes
/// (00 00 03 is 00 00 in the payload, H.264 7.4.1) as it goes, so nothing is copied. Reading past the end gives zeros
/// and sets <see cref="Overran"/>.
/// </summary>
public ref struct RbspBitReader
{
    private const int BitsPerByte = 8;
    private const int EmulationPreventionByte = 3;
    private const int ZerosBeforeEmulationPrevention = 2;

    // ue(v) of 32 bits holds every value H.264 codes; a longer prefix is a damaged unit.
    private const int MaxExpGolombZeros = 31;

    private readonly ReadOnlySpan<byte> _data;
    private int _position;
    private int _zeros;
    private int _current;
    private int _bitsLeft;

    public RbspBitReader(ReadOnlySpan<byte> payload)
    {
        _data = payload;
    }

    /// <summary>Whether a read ran past the payload's end (or met an impossible Exp-Golomb code).</summary>
    public bool Overran { get; private set; }

    /// <summary>u(1).</summary>
    public int ReadBit()
    {
        if (_bitsLeft == 0 && !TryLoadByte())
        {
            Overran = true;

            return 0;
        }

        _bitsLeft--;

        return (_current >> _bitsLeft) & 1;
    }

    /// <summary>u(1) as a flag.</summary>
    public bool ReadFlag()
    {
        return ReadBit() == 1;
    }

    /// <summary>u(n), n at most 32.</summary>
    public uint ReadBits(int count)
    {
        var value = 0u;

        for (var index = 0; index < count; index++)
        {
            value = (value << 1) | (uint)ReadBit();
        }

        return value;
    }

    /// <summary>ue(v): unsigned Exp-Golomb (H.264 9.1).</summary>
    public uint ReadUnsignedExpGolomb()
    {
        var zeros = 0;

        while (ReadBit() == 0)
        {
            if (Overran || zeros == MaxExpGolombZeros)
            {
                Overran = true;

                return 0;
            }

            zeros++;
        }

        var prefix = (1UL << zeros) - 1;

        return (uint)(prefix + ReadBits(zeros));
    }

    /// <summary>se(v): signed Exp-Golomb (H.264 9.1.1), 1, -1, 2, -2 for codes 1, 2, 3, 4.</summary>
    public int ReadSignedExpGolomb()
    {
        var code = (long)ReadUnsignedExpGolomb();

        if ((code & 1) == 1)
        {
            return (int)((code + 1) >> 1);
        }

        return (int)-(code >> 1);
    }

    private bool TryLoadByte()
    {
        if (_position >= _data.Length)
        {
            return false;
        }

        var value = _data[_position];
        _position++;

        if (_zeros >= ZerosBeforeEmulationPrevention && value == EmulationPreventionByte)
        {
            _zeros = 0;

            if (_position >= _data.Length)
            {
                return false;
            }

            value = _data[_position];
            _position++;
        }

        _zeros = value == 0 ? _zeros + 1 : 0;
        _current = value;
        _bitsLeft = BitsPerByte;

        return true;
    }
}
