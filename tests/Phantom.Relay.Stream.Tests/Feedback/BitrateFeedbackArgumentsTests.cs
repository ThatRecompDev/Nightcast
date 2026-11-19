// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Feedback;

namespace Phantom.Relay.Stream.Tests.Feedback;

public sealed class BitrateFeedbackArgumentsTests
{
    [Fact]
    public void Without_switches_the_feedback_is_off()
    {
        Assert.True(BitrateFeedbackArguments.TryParse(["P1440", "--view"], out var feedback, out string? error));
        Assert.Same(BitrateFeedback.Off, feedback);
        Assert.Null(error);
        Assert.Equal("off", feedback.Describe());
    }

    [Fact]
    public void The_switches_combine_in_any_order_among_other_arguments()
    {
        Assert.True(BitrateFeedbackArguments.TryParse(["--twcc", "P1440", "--remb", "60000", "--bandwidth-line", "80000"], out var feedback, out _));

        Assert.Equal(new BitrateFeedback { BandwidthKbps = 80000, RembKbps = 60000, TransportWideCc = true }, feedback);
        Assert.Equal("b=AS 80000 kbit/s, REMB 60000 kbit/s, transport-cc", feedback.Describe());
    }

    [Theory]
    [InlineData("--remb")]
    [InlineData("--remb", "fast")]
    [InlineData("--bandwidth-line", "0")]
    [InlineData("--bandwidth-line", "-5")]
    [InlineData("--bandwidth-line", "1000001")]
    public void A_missing_or_wrong_bitrate_is_refused_with_why(params string[] args)
    {
        Assert.False(BitrateFeedbackArguments.TryParse(args, out _, out string? error));
        Assert.Contains("kbit/s", error, StringComparison.Ordinal);
    }
}
