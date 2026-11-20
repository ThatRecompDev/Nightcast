// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.App.Recording;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// One recording, from F9 to F9: its folder, its two files' recorders and what the window showed with as it started.
/// <see cref="Attach"/> starts their threads, taps the stream's units and the pictures shown, and asks the console for a
/// key frame so the files start at once; <see cref="Finish"/> lets go of both taps and finishes both files.
/// </summary>
internal sealed class RecordingSession
{
    private readonly AccessUnitTap _units;
    private readonly ShownPictureTap _shownTap;
    private readonly SourceRecorder _source;
    private readonly ShownRecorder _shown;
    private readonly DateTimeOffset _startedAt;

    /// <summary>A recording into <paramref name="folder"/> of the stream as <paramref name="stats"/> and <paramref name="settings"/> describe it.</summary>
    public RecordingSession(string folder, RecordingSettings settings, StreamStats stats, AccessUnitTap units, ShownPictureTap shown, TimeProvider clock, ILogger log)
    {
        Folder = folder;
        Settings = settings;
        Multiplier = CompareLabels.Multiplier(settings);
        ShownRate = CompareLabels.ShownRate(Multiplier);
        SourceLabel = CompareLabels.Source(stats.NewPicturesPerSecond);
        ShownLabel = CompareLabels.Shown(settings);

        _units = units;
        _shownTap = shown;
        _startedAt = clock.GetLocalNow();
        _source = new SourceRecorder(Path.Combine(folder, RecordingFolder.SourceFile), stats.Width, stats.Height, units, clock, log);
        _shown = new ShownRecorder(Path.Combine(folder, RecordingFolder.ShownFile), ShownRate, _source, clock, log);
    }

    public string Folder { get; }

    public RecordingSettings Settings { get; }

    public int Multiplier { get; }

    public int ShownRate { get; }

    public string SourceLabel { get; }

    public string ShownLabel { get; }

    /// <summary>Whether the first key frame came.</summary>
    public bool HasStarted => _source.HasStarted;

    /// <summary>When it came (the clock's timestamp).</summary>
    public long Origin => _source.Origin;

    /// <summary>Both writers run, both taps lead to them, and a key frame is asked for.</summary>
    public void Attach()
    {
        _source.Start();
        _shown.Start();
        _units.Attach(_source);
        _shownTap.Attach(_shown);
        _units.RequestKeyframe();
    }

    /// <summary>Both taps let go, both files finished and read back; how the recording came out. Not on the render thread.</summary>
    public RecordingSummary Finish()
    {
        _units.Detach();
        _shownTap.Detach();

        var source = _source.Finish();
        var shown = _shown.Finish();

        return new RecordingSummary(_startedAt, Settings, Multiplier, ShownRate, SourceLabel, ShownLabel, source, shown);
    }
}
