// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Tests;

public sealed class AnnexBUnitsTests
{
    // A parameter set after a long start code, a picture parameter set after a short one, an IDR slice after a long one.
    private static readonly byte[] KeyFrame = [0, 0, 0, 1, 0x67, 0xAA, 0, 0, 1, 0x68, 0xBB, 0, 0, 0, 1, 0x65, 0xCC, 0x00];

    [Fact]
    public void Units_come_out_in_order_without_their_start_codes_or_trailing_zeros()
    {
        var position = 0;
        var units = new List<byte[]>();

        while (AnnexBUnits.TryNext(KeyFrame, ref position, out var unit))
        {
            units.Add(unit.ToArray());
        }

        Assert.Equal(3, units.Count);
        Assert.Equal([0x67, 0xAA], units[0]);
        Assert.Equal([0x68, 0xBB], units[1]);
        Assert.Equal([0x65, 0xCC], units[2]);
    }

    [Fact]
    public void The_sequence_parameter_set_is_found_by_its_type()
    {
        var found = AnnexBUnits.TryFind(KeyFrame, AnnexBUnits.SequenceParameterSetType, out var unit);

        Assert.True(found);
        Assert.Equal([0x67, 0xAA], unit.ToArray());
    }

    [Fact]
    public void An_idr_slice_makes_a_key_frame_and_a_plain_slice_does_not()
    {
        byte[] delta = [0, 0, 0, 1, 0x41, 0x9A, 0x01];

        Assert.True(AnnexBUnits.IsKeyFrame(KeyFrame));
        Assert.False(AnnexBUnits.IsKeyFrame(delta));
    }

    [Fact]
    public void Bytes_without_a_start_code_hold_no_unit()
    {
        byte[] bytes = [0x67, 0x00, 0x00, 0x02, 0x01];
        var position = 0;

        var found = AnnexBUnits.TryNext(bytes, ref position, out var unit);

        Assert.False(found);
        Assert.True(unit.IsEmpty);
        Assert.Equal(bytes.Length, position);
    }

    [Fact]
    public void An_empty_unit_has_no_type()
    {
        Assert.Equal(-1, AnnexBUnits.TypeOf([]));
        Assert.Equal(AnnexBUnits.IdrSliceType, AnnexBUnits.TypeOf([0x65]));
    }
}
