// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.DecodeProbe.Tests;

public sealed class AccessUnitSplitterTests
{
    [Fact]
    public void EachDelimiterStartsAnAccessUnit()
    {
        // AUD, SPS, IDR slice | AUD, slice: hand-made NAL headers with a byte of payload each.
        byte[] stream =
        [
            0, 0, 0, 1, 0x09, 0xF0,
            0, 0, 0, 1, 0x67, 0x42,
            0, 0, 1, 0x65, 0x88,
            0, 0, 0, 1, 0x09, 0xF0,
            0, 0, 1, 0x41, 0x9A,
        ];

        var units = AccessUnitSplitter.Split(stream);

        Assert.Equal(2, units.Count);
        Assert.Equal((1, 17), units[0].GetOffsetAndLength(stream.Length));
        Assert.Equal((18, stream.Length - 18), units[1].GetOffsetAndLength(stream.Length));
    }

    [Fact]
    public void AStreamWithoutDelimitersHasNoAccessUnits()
    {
        byte[] stream = [0, 0, 1, 0x65, 0x88];

        Assert.Empty(AccessUnitSplitter.Split(stream));
    }
}
