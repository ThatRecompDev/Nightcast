// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The probe's bitrate, second by second: each sample gives the video and sound received since the one before (Mbit/s
/// of RTP payload) and the frames with their average size, and the seconds that carried video add up to the run's
/// average and peak. The first sample only starts the count; the seconds before the console streams are left out.
/// </summary>
public sealed class BitrateTally
{
    private readonly TimeProvider _clock;

    private long _lastAt;
    private long _video;
    private long _audio;
    private long _units;
    private bool _started;
    private long _videoCounted;
    private long _audioCounted;
    private double _secondsCounted;

    public BitrateTally(TimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary>The highest video bitrate of one sample, in Mbit/s.</summary>
    public double PeakVideo { get; private set; }

    /// <summary>The video bitrate over the seconds that carried video, in Mbit/s.</summary>
    public double AverageVideo => StreamStatsMeter.Megabits(_videoCounted, _secondsCounted);

    public double AverageAudio => StreamStatsMeter.Megabits(_audioCounted, _secondsCounted);

    /// <summary>The time since the last sample as a line; null for the first sample, which starts the count.</summary>
    public string? Sample(MediaCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        long now = _clock.GetTimestamp();
        double seconds = _clock.GetElapsedTime(_lastAt, now).TotalSeconds;
        bool first = !_started;

        long video = counts.VideoBytes - _video;
        long audio = counts.AudioBytes - _audio;
        long units = counts.AccessUnits - _units;

        (_lastAt, _video, _audio, _units, _started) = (now, counts.VideoBytes, counts.AudioBytes, counts.AccessUnits, true);

        if (first || seconds <= 0)
        {
            return null;
        }

        double videoMegabits = StreamStatsMeter.Megabits(video, seconds);
        Count(video, audio, seconds, videoMegabits);

        return string.Create(CultureInfo.InvariantCulture, $"    video {videoMegabits:F1} Mbit/s, {units} frames of {Average(video, units)} bytes on average; sound {StreamStatsMeter.Megabits(audio, seconds):F2} Mbit/s");
    }

    /// <summary>The average of <paramref name="bytes"/> over <paramref name="units"/>, 0 without any.</summary>
    public static long Average(long bytes, long units)
    {
        if (units <= 0)
        {
            return 0;
        }

        return bytes / units;
    }

    private void Count(long video, long audio, double seconds, double videoMegabits)
    {
        if (video <= 0)
        {
            return;
        }

        _videoCounted += video;
        _audioCounted += audio;
        _secondsCounted += seconds;
        PeakVideo = Math.Max(PeakVideo, videoMegabits);
    }
}
