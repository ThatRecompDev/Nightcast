// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class RecordingFolderTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 21, 4, 3, TimeSpan.FromHours(1));

    [Fact]
    public void A_finished_recording_carries_an_empty_done_marker()
    {
        var folder = Directory.CreateTempSubdirectory("relay-done-").FullName;

        try
        {
            RecordingFolder.MarkDone(folder);

            Assert.Equal(0, new FileInfo(Path.Combine(folder, "done")).Length);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_recording_is_named_by_its_local_start_to_the_second()
    {
        Assert.Equal("2026-11-20 21-04-03", RecordingFolder.Stamp(Start));
    }

    [Fact]
    public void Its_folder_is_under_the_apps_folder_in_the_videos_folder()
    {
        var folder = RecordingFolder.For("videos", Start, _ => false);

        Assert.Equal(Path.Combine("videos", "Nightcast", "2026-11-20 21-04-03"), folder);
    }

    [Fact]
    public void A_taken_name_gets_the_next_free_number()
    {
        var taken = new HashSet<string>
        {
            Path.Combine("videos", "Nightcast", "2026-11-20 21-04-03"),
            Path.Combine("videos", "Nightcast", "2026-11-20 21-04-03 (2)"),
        };

        var folder = RecordingFolder.For("videos", Start, taken.Contains);

        Assert.Equal(Path.Combine("videos", "Nightcast", "2026-11-20 21-04-03 (3)"), folder);
    }

    [Fact]
    public void The_window_shows_the_folder_shortened()
    {
        var shown = RecordingFolder.Short(Path.Combine("videos", "Nightcast", "2026-11-20 21-04-03"));

        Assert.Equal(Path.Combine("…", "Nightcast", "2026-11-20 21-04-03"), shown);
    }
}
