// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.App.Recording;
using Phantom.Relay.Stream;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// The desktop's recorder (F9; docs/architecture/recorder.md). A recording writes into its own folder under the user's
/// Videos folder (<see cref="RecordingFolder"/>): source.mp4, the console's H.264 as it came (<see cref="SourceRecorder"/>),
/// and shown.mp4, every picture Relay showed, encoded on the GPU (<see cref="ShownRecorder"/>). Stopping it finishes both
/// files on a thread of its own and writes recording.txt; then, where the user has ffmpeg, the two are put side by side
/// (compare.mp4, and compare-half-speed.mp4 at half speed) on that thread too. The window reads <see cref="Status"/> each
/// frame; nothing here waits on the render thread.
/// </summary>
internal sealed class StreamRecorder : IDesktopRecorder
{
    private const string NothingYet = "nothing to record yet: no picture was decoded";

    /// <summary>The recorder writes through Media Foundation and D3D11: Windows only.</summary>
    private const string WindowsOnly = "recording is Windows-only (Media Foundation)";
    private const string SavedTo = "saved to ";
    private const string Made = "compare.mp4 made";
    private const string NotCompared = "no compare: ";
    private const int Runs = 2;
    private const int Whole = 100;

    private readonly StreamConnection _connection;
    private readonly ShownPictureTap _shown;
    private readonly RecordingPlaces _places;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly RecorderMachine _machine;
    private readonly Lock _gate = new();
    private RecordingSession? _session;
    private Task _finishing = Task.CompletedTask;

    public StreamRecorder(StreamConnection connection, ShownPictureTap shown, RecordingPlaces places, TimeProvider clock, ILogger<StreamRecorder> log)
    {
        _connection = connection;
        _shown = shown;
        _places = places;
        _clock = clock;
        _log = log;
        _machine = new RecorderMachine(clock);
    }

    public string? Status
    {
        get
        {
            var session = _session;

            if (session is not null && session.HasStarted && _machine.Phase == RecorderPhase.WaitingForKeyFrame)
            {
                _machine.KeyFrame(session.Origin);
            }

            return _machine.Status();
        }
    }

    /// <summary>Whether a recording is still being finished or compared.</summary>
    public bool IsFinishing
    {
        get
        {
            lock (_gate)
            {
                return !_finishing.IsCompleted;
            }
        }
    }

    public void Toggle(RecordingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_session is not null)
        {
            StopRunning();

            return;
        }

        if (_machine.Phase == RecorderPhase.Idle)
        {
            Start(settings);
        }
    }

    public void Stop()
    {
        if (_session is not null)
        {
            StopRunning();
        }
    }

    /// <summary>Waits until every recording stopped so far is finished and compared (the head, once the window closed).</summary>
    public void WaitUntilDone()
    {
        Task finishing;

        lock (_gate)
        {
            finishing = _finishing;
        }

        finishing.GetAwaiter().GetResult();
    }

    private void Start(RecordingSettings settings)
    {
        if (!OperatingSystem.IsWindows())
        {
            Refuse(WindowsOnly);

            return;
        }

        var stats = _connection.Stats;

        if (stats.Width == 0 || stats.Height == 0)
        {
            Refuse(NothingYet);

            return;
        }

        var folder = RecordingFolder.For(_places.Videos, _clock.GetLocalNow(), Directory.Exists);

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (IOException error)
        {
            Refuse(error.Message);

            return;
        }

        var session = new RecordingSession(folder, settings, stats, _connection.AccessUnits, _shown, _clock, _log);
        _ = _machine.TryStart();
        session.Attach();
        _session = session;

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.RecordingStarted(RecordingFolder.Short(folder));
        }
    }

    private void Refuse(string reason)
    {
        _machine.Refused(reason);
        _log.RecordingRefused(reason);
    }

    private void StopRunning()
    {
        var session = _session!;
        _session = null;
        _ = _machine.TryStop();

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.RecordingStopped(RecordingFolder.Short(session.Folder));
        }

        lock (_gate)
        {
            var before = _finishing;
            _finishing = Task.WhenAll(before, Task.Run(() => Finish(session)));
        }
    }

    // The finishing thread: both files, recording.txt, then the comparison where ffmpeg is.
    private void Finish(RecordingSession session)
    {
        try
        {
            var summary = session.Finish();
            var folder = RecordingFolder.Short(session.Folder);
            var compare = CompareNote(summary);
            Report(session.Folder, summary, compare);
            RecordingFolder.MarkDone(session.Folder);

            _machine.Saved(SavedTo + folder);
            _log.RecordingSaved(folder);

            Compare(session.Folder, summary, SavedTo + folder, compare);
        }
        catch (Exception error)
        {
            _machine.Saved($"recording failed: {error.Message}");
            _log.RecordingFailed(error.Message);
        }
    }

    // What the comparison will be: made by ffmpeg, or skipped and why.
    private string? CompareNote(RecordingSummary summary)
    {
        if (_places.Ffmpeg is null)
        {
            return FfmpegLocator.NotFound;
        }

        if (summary.Source.Facts is null || summary.Shown.Facts is null)
        {
            return "both files are needed";
        }

        return null;
    }

    private static void Report(string folder, RecordingSummary summary, string? skipped)
    {
        var compare = skipped is null ? "made by ffmpeg after the recording" : $"not made: {skipped}";
        File.WriteAllText(Path.Combine(folder, RecordingFolder.ReportFile), RecordingReport.Text(summary, compare));
    }

    private void Compare(string folder, RecordingSummary summary, string saved, string? skipped)
    {
        if (skipped is not null)
        {
            _machine.Compared($"{saved} | {NotCompared}{skipped}");
            _log.CompareSkipped(skipped);

            return;
        }

        var started = _clock.GetTimestamp();
        var problem = RunBoth(folder, summary);

        if (problem is null)
        {
            _machine.Compared($"{saved} | {Made}");
            LogCompared(started);

            return;
        }

        File.AppendAllText(Path.Combine(folder, RecordingFolder.ReportFile), $"comparison failed: {problem}{Environment.NewLine}");
        _machine.Compared($"{saved} | compare failed");
        _log.CompareFailed(problem);
    }

    private void LogCompared(long started)
    {
        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.Compared(RecordingFolder.CompareFile, _clock.GetElapsedTime(started).TotalSeconds);
        }
    }

    // compare.mp4, then compare-half-speed.mp4: the progress runs over both.
    private string? RunBoth(string folder, RecordingSummary summary)
    {
        var runner = new CompareRunner(_places.Ffmpeg!);
        var length = summary.Source.Facts!.Duration;
        var request = Request(folder, summary, RecordingFolder.CompareFile, halfSpeed: false);
        var problem = runner.Run(request, length, percent => _machine.Comparing(percent / Runs));

        if (problem is not null)
        {
            return problem;
        }

        var slow = request with { Output = Path.Combine(folder, RecordingFolder.HalfSpeedFile), HalfSpeed = true };

        return runner.Run(slow, length * Runs, percent => _machine.Comparing((Whole + percent) / Runs));
    }

    private CompareRequest Request(string folder, RecordingSummary summary, string file, bool halfSpeed)
    {
        var source = summary.Source.Facts!;

        return new CompareRequest(
            Path.Combine(folder, RecordingFolder.SourceFile),
            Path.Combine(folder, RecordingFolder.ShownFile),
            Path.Combine(folder, file),
            summary.Shown.Offset,
            source.Width,
            source.Height,
            summary.ShownRate,
            summary.SourceLabel,
            summary.ShownLabel,
            _places.FontFile,
            halfSpeed);
    }
}
