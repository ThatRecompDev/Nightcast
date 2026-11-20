// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Time;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Tests.Fakes;

namespace Phantom.Relay.Stream.Tests.Media;

public sealed class DecodeWorkerTests
{
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void While_the_decoder_shares_each_picture_is_handed_on_on_the_gpu_and_never_read_back()
    {
        var decoder = new FakeGpuDecoder { IsSharing = true };
        var mailbox = new PictureMailbox();

        Run(decoder, mailbox, new AccessUnitTap(), units: 2);

        Assert.Equal(["prepare", "decode", "publish", "prepare", "decode", "publish"], decoder.Calls);
        Assert.Equal(2, decoder.PublishedAt.Count);
        Assert.Equal([PictureKind.New, PictureKind.New], decoder.Kinds);
        Assert.Equal([0u, 1u], decoder.RtpTimestamps);
        Assert.False(mailbox.TryTake());
    }

    [Fact]
    public void Until_the_decoder_shares_the_pictures_are_read_back()
    {
        var decoder = new FakeGpuDecoder();

        Run(decoder, new PictureMailbox(), new AccessUnitTap(), units: 1);

        Assert.Equal(["prepare", "decode", "take"], decoder.Calls);
    }

    [Fact]
    public void A_shared_picture_counts_with_its_size()
    {
        var decoder = new FakeGpuDecoder { IsSharing = true };
        var worker = new DecodeWorker(decoder, new PictureMailbox(), new AccessUnitTap(), _clock);
        worker.Start();
        worker.Enqueue([0, 0, 0, 1, 0x65], 0, key: true);

        worker.Dispose();

        Assert.Equal((1L, 2560, 1440), (worker.Pictures, worker.Width, worker.Height));
    }

    [Fact]
    public void A_listener_on_the_tap_sees_every_unit_with_its_timestamp_and_key_flag()
    {
        var tap = new AccessUnitTap();
        var listener = new RecordingListener();
        tap.Attach(listener);

        Run(new FakeGpuDecoder { IsSharing = true }, new PictureMailbox(), tap, units: 3);

        Assert.Equal([(5, 0u, true), (5, 1u, false), (5, 2u, false)], listener.Units);
    }

    private void Run(FakeGpuDecoder decoder, PictureMailbox mailbox, AccessUnitTap tap, int units)
    {
        var worker = new DecodeWorker(decoder, mailbox, tap, _clock);
        worker.Start();

        for (var unit = 0; unit < units; unit++)
        {
            worker.Enqueue([0, 0, 0, 1, 0x65], (uint)unit, key: unit == 0);
        }

        worker.Dispose();
    }

    private sealed class RecordingListener : IAccessUnitListener
    {
        public List<(int Length, uint Timestamp, bool Key)> Units { get; } = [];

        public void Received(byte[] unit, uint rtpTimestamp, bool key)
        {
            Units.Add((unit.Length, rtpTimestamp, key));
        }
    }
}
