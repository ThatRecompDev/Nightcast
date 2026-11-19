// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Testing.Allocations;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Tests.Fakes;
using Phantom.Relay.WebRtc.Rtp;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.Stream.Tests.Media;

/// <summary>The bitrate feedback on the wire: what goes out when, protected as SRTCP, read back with the same key.</summary>
public sealed class FeedbackSenderTests
{
    private const uint Me = 0x52454C59;
    private const uint Video = 0xA0B0C0D0;
    private const int ExtensionId = 3;

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingRtcp _rtcp = new();
    private readonly SrtpContext _console;
    private readonly SrtpContext _protect;

    private TimeSpan _elapsed;

    public FeedbackSenderTests()
    {
        var key = new SrtpMasterKey(Convert.FromHexString("000102030405060708090a0b0c0d0e0f"), new byte[SrtpSessionKeys.SaltLength(SrtpProfile.AeadAes128Gcm)]);
        _protect = SrtpContext.Create(SrtpProfile.AeadAes128Gcm, key, 8);
        _console = SrtpContext.Create(SrtpProfile.AeadAes128Gcm, key, 8);
    }

    [Fact]
    public void Off_nothing_goes_out()
    {
        var sender = Sender(BitrateFeedback.Off, ExtensionId);
        var header = new RtpHeader();
        var packet = OneBytePacket(1);
        header.TryRead(packet);

        sender.Record(packet, header);
        sender.Service(Video);
        _clock.Advance(TimeSpan.FromSeconds(2));
        sender.Service(Video);

        Assert.Empty(_rtcp.Sent);
    }

    [Fact]
    public void A_remb_goes_out_once_the_video_is_known_and_then_once_a_second()
    {
        var sender = Sender(new BitrateFeedback { RembKbps = 80000 }, 0);

        sender.Service(0);
        sender.Service(Video);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        sender.Service(Video);

        Assert.Single(_rtcp.Sent);
        Assert.Equal("8FCE0005" + "52454C59" + "00000000" + "52454D42" + "0126625A" + "A0B0C0D0", Unprotect(_rtcp.Sent[0]));

        _clock.Advance(TimeSpan.FromMilliseconds(500));
        sender.Service(Video);

        Assert.Equal(2, _rtcp.Sent.Count);
        Assert.Equal(2, sender.RembSent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Transport_feedback_reports_the_packets_carrying_the_answers_extension_in_either_form(bool twoByte)
    {
        var sender = Sender(new BitrateFeedback { TransportWideCc = true }, ExtensionId);

        // 7 at 5 ms (20 units after the reference time 0), 8 at 6 ms (4 more); the feedback is due at 50 ms.
        Arrive(sender, 7, 5, twoByte);
        Arrive(sender, 8, 6, twoByte);
        sender.Service(Video);

        Assert.Empty(_rtcp.Sent);

        At(50);
        sender.Service(Video);

        Assert.Equal("8FCD0005" + "52454C59" + "A0B0C0D0" + "00070002" + "00000000" + "B000" + "1404", Unprotect(Assert.Single(_rtcp.Sent)));
        Assert.Equal(1, sender.TransportFeedbackSent);
    }

    [Fact]
    public void Transport_feedback_waits_its_interval_and_needs_new_packets()
    {
        var sender = Sender(new BitrateFeedback { TransportWideCc = true }, ExtensionId);
        Arrive(sender, 1, 50, false);
        sender.Service(Video);
        Arrive(sender, 2, 60, false);
        sender.Service(Video);

        Assert.Single(_rtcp.Sent);

        At(100);
        sender.Service(Video);
        sender.Service(Video);

        Assert.Equal(2, _rtcp.Sent.Count);
    }

    [Fact]
    public void Without_the_extension_in_the_answer_no_transport_feedback_goes_out()
    {
        var sender = Sender(new BitrateFeedback { TransportWideCc = true }, 0);

        Arrive(sender, 1, 100, false);
        sender.Service(Video);

        Assert.Empty(_rtcp.Sent);
    }

    [Fact]
    public void Recording_an_arrival_allocates_nothing()
    {
        var sender = Sender(new BitrateFeedback { TransportWideCc = true }, ExtensionId);
        var header = new RtpHeader();
        var packet = OneBytePacket(1);
        header.TryRead(packet);

        var report = AllocationMeter.Measure(() => sender.Record(packet, header));

        Assert.Equal(0, report.BytesPerRun);
    }

    private FeedbackSender Sender(BitrateFeedback feedback, int extensionId)
    {
        return new FeedbackSender(feedback, extensionId, Me, _protect, _rtcp, _clock);
    }

    private void Arrive(FeedbackSender sender, ushort sequence, int milliseconds, bool twoByte)
    {
        var header = new RtpHeader();
        var packet = twoByte ? TwoBytePacket(sequence) : OneBytePacket(sequence);
        At(milliseconds);

        Assert.True(header.TryRead(packet));
        sender.Record(packet, header);
    }

    /// <summary>An RTP packet with a one-byte extension (0xBEDE): id 3, length 2 (L=1), the sequence, a padding octet.</summary>
    /// <summary>Moves the clock to <paramref name="milliseconds"/> after the test began.</summary>
    private void At(int milliseconds)
    {
        _clock.AdvanceMilliseconds(milliseconds - (int)_elapsed.TotalMilliseconds);
        _elapsed = TimeSpan.FromMilliseconds(milliseconds);
    }

    private static byte[] OneBytePacket(ushort sequence)
    {
        var packet = Header(0xBEDE);
        packet[16] = (ExtensionId << 4) | 1;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(17), sequence);

        return packet;
    }

    /// <summary>An RTP packet with a two-byte extension (0x1000): id 3, length 2, the sequence.</summary>
    private static byte[] TwoBytePacket(ushort sequence)
    {
        var packet = Header(0x1000);
        packet[16] = ExtensionId;
        packet[17] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(18), sequence);

        return packet;
    }

    /// <summary>V=2 with X set, payload type 104, sequence 9, the video SSRC, a one-word extension, four payload octets.</summary>
    private static byte[] Header(ushort profile)
    {
        var packet = new byte[24];
        packet[0] = 0x90;
        packet[1] = 104;
        packet[3] = 9;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(8), Video);

        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(12), profile);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(14), 1);

        return packet;
    }

    private string Unprotect(byte[] packet)
    {
        Assert.True(_console.TryUnprotectRtcp(packet, out int length));

        return Convert.ToHexString(packet.AsSpan(0, length));
    }
}
