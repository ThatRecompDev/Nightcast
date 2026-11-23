// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.DecodeProbe.Tests;

public sealed class TimingTallyTests
{
    [Fact]
    public void TheFirstPictureIsKeptApart()
    {
        var tally = new TimingTally();

        tally.Add(0, TimeSpan.FromMilliseconds(30));
        tally.Add(1, TimeSpan.FromMilliseconds(2));
        tally.Add(2, TimeSpan.FromMilliseconds(4));

        Assert.Equal(TimeSpan.FromMilliseconds(30), tally.First);
        Assert.Equal(TimeSpan.FromMilliseconds(3), tally.Average);
        Assert.Equal(TimeSpan.FromMilliseconds(4), tally.Worst);
    }
}
