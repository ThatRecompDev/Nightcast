// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Events;

namespace Phantom.Relay.Pads.Linux.Tests.Events;

public sealed class InputEventTests
{
    [Fact]
    public void An_event_is_read_from_its_24_bytes_past_the_time()
    {
        var bytes = new byte[24];
        bytes[0] = 0xAA;
        bytes[15] = 0xBB;

        bytes[16] = 0x03;
        bytes[18] = 0x01;
        bytes[20] = 0x00;
        bytes[21] = 0x80;
        bytes[22] = 0xFF;
        bytes[23] = 0xFF;

        var input = InputEvent.Read(bytes);

        Assert.Equal(new InputEvent(EvdevCodes.Absolute, EvdevCodes.AxisY, -32768), input);
    }

    [Fact]
    public void An_event_written_has_a_zero_time_and_reads_back()
    {
        var bytes = Enumerable.Repeat((byte)0x55, 24).ToArray();
        var input = new InputEvent(EvdevCodes.ForceFeedback, 7, 1);

        input.Write(bytes);

        Assert.All(bytes[..16], static value => Assert.Equal(0, value));
        Assert.Equal(new byte[] { 0x15, 0x00, 0x07, 0x00, 0x01, 0x00, 0x00, 0x00 }, bytes[16..]);
        Assert.Equal(input, InputEvent.Read(bytes));
    }
}
