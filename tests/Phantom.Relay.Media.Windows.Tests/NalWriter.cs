// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Tests;

/// <summary>
/// Writes a hand-made NAL unit bit by bit (u(n), ue(v), se(v)), then gives it with its header byte, the stop bit and
/// the emulation prevention bytes an encoder would put in, so a test states its sequence parameter set field by field.
/// </summary>
public sealed class NalWriter
{
    private readonly List<bool> _bits = [];

    public NalWriter Bits(uint value, int count)
    {
        for (var bit = count - 1; bit >= 0; bit--)
        {
            _bits.Add(((value >> bit) & 1) == 1);
        }

        return this;
    }

    public NalWriter Flag(bool value)
    {
        _bits.Add(value);

        return this;
    }

    public NalWriter Ue(uint value)
    {
        var code = value + 1;
        var length = 32 - uint.LeadingZeroCount(code);

        return Bits(0, (int)length - 1).Bits(code, (int)length);
    }

    public NalWriter Se(int value)
    {
        if (value > 0)
        {
            return Ue((uint)((2 * value) - 1));
        }

        return Ue((uint)(-2 * value));
    }

    /// <summary>The unit: header byte, payload with its stop bit, emulation prevention after every two zero bytes.</summary>
    public byte[] ToUnit(int type)
    {
        var payload = new List<bool>(_bits) { true };

        while (payload.Count % 8 != 0)
        {
            payload.Add(false);
        }

        var unit = new List<byte> { (byte)(0x60 | type) };
        var zeros = 0;

        for (var index = 0; index < payload.Count; index += 8)
        {
            var value = 0;

            for (var bit = 0; bit < 8; bit++)
            {
                if (payload[index + bit])
                {
                    value |= 0x80 >> bit;
                }
            }

            if (zeros >= 2 && value <= 3)
            {
                unit.Add(3);
                zeros = 0;
            }

            unit.Add((byte)value);
            zeros = value == 0 ? zeros + 1 : 0;
        }

        return [.. unit];
    }
}
