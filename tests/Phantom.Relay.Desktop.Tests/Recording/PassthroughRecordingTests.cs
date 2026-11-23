// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Desktop.Tests.Recording;

/// <summary>A recording end to end on hand-made units and packets, into a folder under the system's temporary folder.</summary>
public sealed class PassthroughRecordingTests : IDisposable
{
    private static readonly byte[] Sps = [0x67, 0x64, 0x00, 0x1F, 0xAC, 0xD9, 0x40, 0x50];
    private static readonly byte[] Pps = [0x68, 0xEB, 0xE3, 0xCB];

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Phantom.Relay.Tests", "passthrough-" + Guid.NewGuid().ToString("N"));

    public PassthroughRecordingTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void A_recording_starts_at_a_key_frame_and_ends_as_source_mp4_then_done()
    {
        var tap = new AccessUnitTap();
        using var recording = new PassthroughRecording(_folder, 1280, 720, tap, TimeProvider.System, NullLogger.Instance);
        recording.Start();

        recording.Received([0xF4, 1, 2], 5000);
        recording.Received(Delta(), 1000, key: false);
        recording.Received(Key(), 3000, key: true);
        recording.Received(Delta(), 4500, key: false);

        recording.Received([0xF4, 1, 2], 9000);
        recording.Received([0xF4, 3, 4], 9480);

        var outcome = recording.Finish();

        Assert.Null(outcome.Problem);
        Assert.Equal(1, outcome.SkippedUnits);
        Assert.Equal(2, outcome.Counts!.VideoSamples);
        Assert.Equal(2, outcome.Counts.AudioPackets);

        Assert.True(File.Exists(Path.Combine(_folder, "source.mp4")));
        Assert.False(File.Exists(Path.Combine(_folder, "source.mp4.part")));
        Assert.Equal(0, new FileInfo(Path.Combine(_folder, "done")).Length);

        var file = File.ReadAllBytes(Path.Combine(_folder, "source.mp4"));
        Assert.Equal("ftyp"u8.ToArray(), file.AsSpan(4, 4).ToArray());
        Assert.Equal((uint)file.Length - BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(file.Length - 4)), FindLast(file, "mfra"u8) - 4);
    }

    [Fact]
    public void No_key_frame_makes_no_file_and_says_why()
    {
        using var recording = new PassthroughRecording(_folder, 1280, 720, new AccessUnitTap(), TimeProvider.System, NullLogger.Instance);
        recording.Start();
        recording.Received(Delta(), 1000, key: false);

        var outcome = recording.Finish();

        Assert.Null(outcome.Counts);
        Assert.NotNull(outcome.Problem);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    private static byte[] Key()
    {
        return [0, 0, 0, 1, .. Sps, 0, 0, 0, 1, .. Pps, 0, 0, 1, 0x65, 0x88, 0x84, 0x21];
    }

    private static byte[] Delta()
    {
        return [0, 0, 0, 1, 0x41, 0x9A, 0x05, 0x10];
    }

    private static uint FindLast(byte[] file, ReadOnlySpan<byte> type)
    {
        return (uint)file.AsSpan().LastIndexOf(type);
    }
}
