// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>
/// SRTP's replay list (RFC 3711 section 3.3.2) as a sliding bitmap over packet indices: an index above the highest is
/// new, one that falls behind the window is too old, and one inside it is new when its bit is clear. The check comes
/// before the tag is verified and the bit is set only after (<see cref="Accept"/>), so a forged packet moves nothing.
/// </summary>
public sealed class SrtpReplayWindow
{
    private const int WordBits = 64;

    private readonly ulong[] _bits;
    private readonly int _size;
    private long _highest = -1;

    public SrtpReplayWindow(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, WordBits);

        _bits = new ulong[(size + WordBits - 1) / WordBits];
        _size = _bits.Length * WordBits;
    }

    /// <summary>The highest index accepted, or -1 before the first.</summary>
    public long Highest => _highest;

    /// <summary>The indices the window remembers behind the highest.</summary>
    public int Size => _size;

    /// <summary>True when <paramref name="index"/> has not been accepted and is not too old to tell.</summary>
    [HotPath]
    public bool IsFresh(long index)
    {
        if (index < 0)
        {
            return false;
        }

        if (index > _highest)
        {
            return true;
        }

        if (_highest - index >= _size)
        {
            return false;
        }

        return (_bits[Word(index)] & Bit(index)) == 0;
    }

    /// <summary>Records <paramref name="index"/>, sliding the window forward when it is the new highest.</summary>
    [HotPath]
    public void Accept(long index)
    {
        if (index > _highest)
        {
            Slide(index);
        }

        _bits[Word(index)] |= Bit(index);
    }

    public void Reset()
    {
        Array.Clear(_bits);
        _highest = -1;
    }

    /// <summary>Forgets the indices the window leaves behind as it moves up to <paramref name="index"/>.</summary>
    private void Slide(long index)
    {
        if (index - _highest >= _size)
        {
            Array.Clear(_bits);
        }
        else
        {
            for (long cleared = _highest + 1; cleared <= index; cleared++)
            {
                _bits[Word(cleared)] &= ~Bit(cleared);
            }
        }

        _highest = index;
    }

    private int Word(long index)
    {
        return (int)(index % _size / WordBits);
    }

    private static ulong Bit(long index)
    {
        return 1UL << (int)(index % WordBits);
    }
}
