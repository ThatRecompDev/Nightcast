// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

/// <summary>RFC 3720 appendix B.4's CRC-32C test vectors (iSCSI uses the same checksum as SCTP).</summary>
public sealed class SctpCrc32cTests
{
    [Fact]
    public void Thirty_two_zero_bytes_give_the_rfc_3720_value()
    {
        var data = new byte[32];

        Assert.Equal(0x8A9136AAu, SctpCrc32c.Hash(data));
    }

    [Fact]
    public void Thirty_two_bytes_of_ones_give_the_rfc_3720_value()
    {
        var data = Enumerable.Repeat((byte)0xFF, 32).ToArray();

        Assert.Equal(0x62A8AB43u, SctpCrc32c.Hash(data));
    }

    [Fact]
    public void Thirty_two_incrementing_bytes_give_the_rfc_3720_value()
    {
        var data = Enumerable.Range(0, 32).Select(static i => (byte)i).ToArray();

        Assert.Equal(0x46DD794Eu, SctpCrc32c.Hash(data));
    }

    [Fact]
    public void Thirty_two_decrementing_bytes_give_the_rfc_3720_value()
    {
        var data = Enumerable.Range(0, 32).Select(static i => (byte)(31 - i)).ToArray();

        Assert.Equal(0x113FDB5Cu, SctpCrc32c.Hash(data));
    }

    [Fact]
    public void Updating_in_pieces_gives_the_same_value_as_one_pass()
    {
        var data = Enumerable.Range(0, 45).Select(static i => (byte)(i * 7)).ToArray();

        uint crc = SctpCrc32c.Update(SctpCrc32c.Seed, data.AsSpan(0, 13));
        crc = SctpCrc32c.Update(crc, data.AsSpan(13));

        Assert.Equal(SctpCrc32c.Hash(data), SctpCrc32c.Finish(crc));
    }
}
