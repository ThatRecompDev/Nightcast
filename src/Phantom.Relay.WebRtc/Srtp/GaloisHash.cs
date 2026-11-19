// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// GCM's GHASH (NIST SP 800-38D section 6.4) with one hash key H, fed in pieces: the associated data, a
/// <see cref="Pad"/>, the ciphertext, and the lengths in <see cref="Finish"/>. A block is multiplied by H four bits at
/// a time (Horner's rule from the last nibble) with tables of the 16 nibble multiples of H and of the reduction of the
/// four bits a nibble shift drops. BouncyCastle's own GHASH multipliers are obsolete and its GCM cipher makes new
/// buffers on every nonce, so this runs GCM without allocating; AES itself stays BouncyCastle's.
/// </summary>
public sealed class GaloisHash
{
    private const int BitsPerByte = 8;
    private const int WordBits = 64;
    private const int NibbleBits = 4;
    private const int NibbleMask = 0xF;
    private const int Nibbles = 16;

    /// <summary>The highest nibble value, the coefficient of the lowest power of x: the multiple that is H itself.</summary>
    private const int HighBit = 8;

    /// <summary>x^128 = 1 + x + x^2 + x^7, in GCM's reflected bit order the top byte 0xE1 of a 128-bit value.</summary>
    private const ulong Reduction = 0xE100000000000000;

    private readonly ulong[] _high = new ulong[Nibbles];
    private readonly ulong[] _low = new ulong[Nibbles];
    private readonly ulong[] _dropped = new ulong[Nibbles];
    private readonly byte[] _state = new byte[SrtpFormat.BlockLength];
    private int _filled;

    public GaloisHash(ReadOnlySpan<byte> hashKey)
    {
        BuildMultiples(BinaryPrimitives.ReadUInt64BigEndian(hashKey), BinaryPrimitives.ReadUInt64BigEndian(hashKey[sizeof(ulong)..]));
        BuildReductions();
    }

    /// <summary>Starts a new message.</summary>
    [HotPath]
    public void Reset()
    {
        Array.Clear(_state);
        _filled = 0;
    }

    /// <summary>Adds octets to the current section (associated data or ciphertext).</summary>
    [HotPath]
    public void Absorb(ReadOnlySpan<byte> data)
    {
        foreach (byte value in data)
        {
            _state[_filled] ^= value;
            _filled++;

            if (_filled == SrtpFormat.BlockLength)
            {
                MultiplyByKey();
                _filled = 0;
            }
        }
    }

    /// <summary>Ends a section, zero-padding its last block.</summary>
    [HotPath]
    public void Pad()
    {
        if (_filled == 0)
        {
            return;
        }

        MultiplyByKey();
        _filled = 0;
    }

    /// <summary>Adds the length block (both lengths in bits) and writes the hash.</summary>
    [HotPath]
    public void Finish(int associatedLength, int cipherLength, Span<byte> destination)
    {
        Pad();

        Span<byte> lengths = stackalloc byte[SrtpFormat.BlockLength];
        BinaryPrimitives.WriteUInt64BigEndian(lengths, (ulong)associatedLength * BitsPerByte);
        BinaryPrimitives.WriteUInt64BigEndian(lengths[sizeof(ulong)..], (ulong)cipherLength * BitsPerByte);
        Absorb(lengths);

        _state.CopyTo(destination);
    }

    /// <summary>The state times H: Z = Z * x^4 + nibble * H for each nibble, the last first.</summary>
    private void MultiplyByKey()
    {
        ulong high = 0;
        ulong low = 0;

        for (int i = SrtpFormat.BlockLength - 1; i >= 0; i--)
        {
            int value = _state[i];
            ShiftNibbleIn(ref high, ref low, value & NibbleMask);
            ShiftNibbleIn(ref high, ref low, value >> NibbleBits);
        }

        BinaryPrimitives.WriteUInt64BigEndian(_state, high);
        BinaryPrimitives.WriteUInt64BigEndian(_state.AsSpan(sizeof(ulong)), low);
    }

    private void ShiftNibbleIn(ref ulong high, ref ulong low, int nibble)
    {
        int dropped = (int)(low & NibbleMask);
        low = (low >> NibbleBits) | (high << (WordBits - NibbleBits));
        high = (high >> NibbleBits) ^ _dropped[dropped];
        high ^= _high[nibble];
        low ^= _low[nibble];
    }

    /// <summary>The multiples of H by each nibble, whose top bit is the coefficient of x^0 and bottom bit of x^3.</summary>
    private void BuildMultiples(ulong high, ulong low)
    {
        for (int bit = HighBit; bit > 0; bit >>= 1)
        {
            _high[bit] = high;
            _low[bit] = low;
            ulong carry = low & 1;
            low = (low >> 1) | (high << (WordBits - 1));
            high = (high >> 1) ^ (Reduction * carry);
        }

        for (int nibble = 1; nibble < Nibbles; nibble++)
        {
            int lowest = nibble & -nibble;
            _high[nibble] = _high[lowest] ^ _high[nibble ^ lowest];
            _low[nibble] = _low[lowest] ^ _low[nibble ^ lowest];
        }
    }

    /// <summary>What the four bits dropped by a shift by x^4 (x^124 to x^127) become: multiples of x^128, reduced.</summary>
    private void BuildReductions()
    {
        for (int dropped = 0; dropped < Nibbles; dropped++)
        {
            ulong reduced = 0;

            for (int bit = 0; bit < NibbleBits; bit++)
            {
                if ((dropped & (1 << bit)) != 0)
                {
                    reduced ^= Reduction >> (NibbleBits - 1 - bit);
                }
            }

            _dropped[dropped] = reduced;
        }
    }
}
