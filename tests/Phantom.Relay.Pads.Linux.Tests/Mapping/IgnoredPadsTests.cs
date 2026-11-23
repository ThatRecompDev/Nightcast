// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Tests.Mapping;

public sealed class IgnoredPadsTests
{
    [Fact]
    public void Steams_list_of_pairs_is_read()
    {
        var ignored = IgnoredPads.Parse("0x28DE/0x1205,0x045e/0x028e, 0x0B05/0x1ABE");

        Assert.True(ignored.Contains(0x28DE, 0x1205));
        Assert.True(ignored.Contains(0x045E, 0x028E));
        Assert.True(ignored.Contains(0x0B05, 0x1ABE));
        Assert.False(ignored.Contains(0x28DE, 0x11FF));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_is_ignored_without_a_list(string? hint)
    {
        Assert.False(IgnoredPads.Parse(hint).Contains(0x28DE, 0x1205));
    }

    [Fact]
    public void An_entry_that_does_not_read_is_skipped()
    {
        var ignored = IgnoredPads.Parse("garbage,0x054C,0x054C/0x0CE6/0x1,0xZZZZ/0x0001,054C/0CE6");

        Assert.True(ignored.Contains(0x054C, 0x0CE6));
        Assert.False(ignored.Contains(0, 1));
    }
}
