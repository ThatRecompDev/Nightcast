// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.WebRtc.Tests.Rtp;

public sealed class RtpSequenceUnwrapperTests
{
    [Fact]
    public void The_count_goes_on_across_a_wrap()
    {
        var unwrapper = new RtpSequenceUnwrapper();

        Assert.Equal(65534, unwrapper.Unwrap(65534));
        Assert.Equal(65535, unwrapper.Unwrap(65535));
        Assert.Equal(65536, unwrapper.Unwrap(0));
        Assert.Equal(65537, unwrapper.Unwrap(1));
    }

    [Fact]
    public void A_late_packet_from_before_the_wrap_lands_before_it()
    {
        var unwrapper = new RtpSequenceUnwrapper();
        unwrapper.Unwrap(65530);
        unwrapper.Unwrap(2);

        long late = unwrapper.Unwrap(65533);

        Assert.Equal(65533, late);
        Assert.Equal(65538, unwrapper.Highest);
    }

    [Fact]
    public void Many_wraps_keep_counting()
    {
        var unwrapper = new RtpSequenceUnwrapper();
        long last = 0;

        for (int i = 0; i < 300_000; i += 1000)
        {
            last = unwrapper.Unwrap((ushort)i);
        }

        Assert.Equal(299_000, last);
    }

    [Fact]
    public void A_packet_from_before_the_first_comes_out_negative()
    {
        var unwrapper = new RtpSequenceUnwrapper();
        unwrapper.Unwrap(3);

        Assert.Equal(-2, unwrapper.Unwrap(65534));
    }
}
