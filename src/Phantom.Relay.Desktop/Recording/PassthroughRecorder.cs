// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.App.Recording;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// The Linux recorder (F9; docs/architecture/recorder.md): source.mp4 alone, the console's H.264 and Opus sound as they
/// came, written by the pure C# writer (<see cref="PassthroughRecording"/>), so a handheld records at almost no cost.
/// Each recording gets its folder under the user's Videos folder (<see cref="Places.UserFolders"/>, then
/// <see cref="RecordingFolder"/>), and the window the same lines as on Windows ("● REC 00:42", "saved to …"). Stopping
/// finishes the file on a thread of its own; nothing here waits on the render thread.
/// </summary>
internal sealed class PassthroughRecorder : IDesktopRecorder
{
    private const string NothingYet = "nothing to record yet: no picture was decoded";
    private const string SavedTo = "saved to ";

    private readonly StreamConnection _connection;
    private readonly AudioPacketTap _sound;
    private readonly string _videos;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly RecorderMachine _machine;
    private readonly Lock _gate = new();
    private PassthroughRecording? _recording;
    private Task _finishing = Task.CompletedTask;

    /// <summary>Records <paramref name="connection"/>'s units and <paramref name="sound"/>'s packets into a folder under <paramref name="videos"/>.</summary>
    public PassthroughRecorder(StreamConnection connection, AudioPacketTap sound, string videos, TimeProvider clock, ILogger<PassthroughRecorder> log)
    {
        _connection = connection;
        _sound = sound;
        _videos = videos;
        _clock = clock;
        _log = log;
        _machine = new RecorderMachine(clock);
    }

    public string? Status
    {
        get
        {
            var recording = _recording;

            if (recording is not null && recording.HasStarted && _machine.Phase == RecorderPhase.WaitingForKeyFrame)
            {
                _machine.KeyFrame(recording.Origin);
            }

            return _machine.Status();
        }
    }

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

        if (_recording is not null)
        {
            StopRunning();

            return;
        }

        if (_machine.Phase == RecorderPhase.Idle)
        {
            Start();
        }
    }

    public void Stop()
    {
        if (_recording is not null)
        {
            StopRunning();
        }
    }

    public void WaitUntilDone()
    {
        Task finishing;

        lock (_gate)
        {
            finishing = _finishing;
        }

        finishing.GetAwaiter().GetResult();
    }

    private void Start()
    {
        var stats = _connection.Stats;

        if (stats.Width == 0 || stats.Height == 0)
        {
            Refuse(NothingYet);

            return;
        }

        var folder = RecordingFolder.For(_videos, _clock.GetLocalNow(), Directory.Exists);

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Refuse(error.Message);

            return;
        }

        Attach(new PassthroughRecording(folder, stats.Width, stats.Height, _connection.AccessUnits, _clock, _log));
    }

    // Its thread runs, both taps lead to it, and a key frame is asked for.
    private void Attach(PassthroughRecording recording)
    {
        _ = _machine.TryStart();
        recording.Start();
        _connection.AccessUnits.Attach(recording);
        _sound.Attach(recording);
        _connection.AccessUnits.RequestKeyframe();
        _recording = recording;

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.RecordingStarted(RecordingFolder.Short(recording.Folder));
        }
    }

    private void Refuse(string reason)
    {
        _machine.Refused(reason);
        _log.RecordingRefused(reason);
    }

    private void StopRunning()
    {
        var recording = _recording!;
        _recording = null;
        _connection.AccessUnits.Detach();
        _sound.Detach();
        _ = _machine.TryStop();

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.RecordingStopped(RecordingFolder.Short(recording.Folder));
        }

        lock (_gate)
        {
            var before = _finishing;
            _finishing = Task.WhenAll(before, Task.Run(() => Finish(recording)));
        }
    }

    // The finishing thread: the file finished, renamed and marked done.
    private void Finish(PassthroughRecording recording)
    {
        try
        {
            var outcome = recording.Finish();
            var folder = RecordingFolder.Short(recording.Folder);

            if (outcome.Counts is null)
            {
                _machine.Saved($"recording failed: {outcome.Problem}");
                _log.RecordingFailed(outcome.Problem ?? string.Empty);

                return;
            }

            _machine.Saved(SavedTo + folder);
            _log.RecordingSaved(folder);
            LogCounts(outcome);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _machine.Saved($"recording failed: {error.Message}");
            _log.RecordingFailed(error.Message);
        }
        finally
        {
            recording.Dispose();
        }
    }

    private void LogCounts(PassthroughOutcome outcome)
    {
        var counts = outcome.Counts!;

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.PassthroughWritten(counts.VideoSamples, counts.AudioPackets, counts.AudioConcealed, counts.Duration.TotalSeconds, outcome.DroppedUnits, outcome.DroppedPackets);
        }
    }
}
