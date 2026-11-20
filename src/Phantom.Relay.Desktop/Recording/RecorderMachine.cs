// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// The recorder's state and what the window says of it. F9 starts a recording (<see cref="TryStart"/>: idle only), which
/// waits for its first key frame (<see cref="KeyFrame"/>) and then records; F9 again stops it (<see cref="TryStop"/>)
/// and it saves until <see cref="Saved"/>. The text (<see cref="Status"/>) is "● REC 00:42" while recording, "saving"
/// while saving, then the result ("saved to …\Phantom.Relay\&lt;stamp&gt;") for <see cref="ResultShownFor"/>, with the
/// comparison's progress while ffmpeg runs (<see cref="Comparing"/>, <see cref="Compared"/>). A comparison of an earlier
/// recording may run while a new one records. Any thread; one short lock. The text is built only when it changes (a
/// recording's seconds, a percent), so the render thread compares references and allocates nothing in between.
/// </summary>
internal sealed class RecorderMachine
{
    /// <summary>How long a result stays on screen.</summary>
    public static readonly TimeSpan ResultShownFor = TimeSpan.FromSeconds(5);

    public const string RecordMark = "● REC";
    public const string WaitingText = RecordMark + " waiting for a key frame";
    public const string SavingText = "saving the recording";

    private const int SecondsPerMinute = 60;
    private const int NoPercent = -1;
    private const int Whole = 100;

    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private RecorderPhase _phase;
    private long _keyAt;
    private string? _result;
    private long _resultAt;
    private int _percent = NoPercent;
    private int _resultVersion;

    // What the cached text was built from.
    private string? _text;
    private bool _builtAny;
    private (RecorderPhase Phase, long Seconds, int Percent, int Version, bool Shown) _built;

    public RecorderMachine(TimeProvider clock)
    {
        _clock = clock;
    }

    public RecorderPhase Phase
    {
        get
        {
            lock (_gate)
            {
                return _phase;
            }
        }
    }

    /// <summary>F9 while idle: the recording waits for its first key frame; false when not idle.</summary>
    public bool TryStart()
    {
        lock (_gate)
        {
            if (_phase != RecorderPhase.Idle)
            {
                return false;
            }

            _phase = RecorderPhase.WaitingForKeyFrame;

            return true;
        }
    }

    /// <summary>The first key frame came at <paramref name="timestamp"/> (the clock's): the recording's time runs from it.</summary>
    public void KeyFrame(long timestamp)
    {
        lock (_gate)
        {
            if (_phase == RecorderPhase.WaitingForKeyFrame)
            {
                _phase = RecorderPhase.Recording;
                _keyAt = timestamp;
            }
        }
    }

    /// <summary>F9 while recording (or waiting): the files are finished; false when nothing records.</summary>
    public bool TryStop()
    {
        lock (_gate)
        {
            if (_phase is not (RecorderPhase.WaitingForKeyFrame or RecorderPhase.Recording))
            {
                return false;
            }

            _phase = RecorderPhase.Saving;

            return true;
        }
    }

    /// <summary>The files are finished (or failed): idle again, <paramref name="result"/> shown for a while.</summary>
    public void Saved(string result)
    {
        lock (_gate)
        {
            _phase = RecorderPhase.Idle;
            Show(result);
        }
    }

    /// <summary>A start that did not happen (nothing to record yet): <paramref name="result"/> says why, for a while.</summary>
    public void Refused(string result)
    {
        lock (_gate)
        {
            Show(result);
        }
    }

    /// <summary>The comparison is <paramref name="percent"/> done.</summary>
    public void Comparing(int percent)
    {
        lock (_gate)
        {
            _percent = Math.Clamp(percent, 0, Whole);
        }
    }

    /// <summary>The comparison ended: <paramref name="result"/> shown for a while.</summary>
    public void Compared(string result)
    {
        lock (_gate)
        {
            Show(result);
        }
    }

    /// <summary>What the window shows now, or null for nothing; the same string until it changes.</summary>
    public string? Status()
    {
        var now = _clock.GetTimestamp();

        lock (_gate)
        {
            var seconds = 0L;

            if (_phase == RecorderPhase.Recording)
            {
                seconds = (long)_clock.GetElapsedTime(_keyAt, now).TotalSeconds;
            }

            var shown = _result is not null && (_percent != NoPercent || _clock.GetElapsedTime(_resultAt, now) < ResultShownFor);
            var key = (_phase, seconds, _percent, _resultVersion, shown);

            if (!_builtAny || key != _built)
            {
                _builtAny = true;
                _built = key;
                _text = Build(seconds, shown);
            }

            return _text;
        }
    }

    /// <summary>"● REC 01:05" for a recording <paramref name="seconds"/> long.</summary>
    public static string RecordingText(long seconds)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{RecordMark} {seconds / SecondsPerMinute:00}:{seconds % SecondsPerMinute:00}");
    }

    private void Show(string result)
    {
        _result = result;
        _resultAt = _clock.GetTimestamp();
        _percent = NoPercent;
        _resultVersion++;
    }

    private string? Build(long seconds, bool shown)
    {
        switch (_phase)
        {
            case RecorderPhase.WaitingForKeyFrame:
                return WaitingText;
            case RecorderPhase.Recording:
                return RecordingText(seconds);
            case RecorderPhase.Saving:
                return SavingText;
            default:
                return Result(shown);
        }
    }

    private string? Result(bool shown)
    {
        if (!shown)
        {
            return null;
        }

        if (_percent != NoPercent)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{_result} | compare {_percent} %");
        }

        return _result;
    }
}
