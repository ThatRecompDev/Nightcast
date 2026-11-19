// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App.Input;
using Phantom.Relay.App.Logging;
using Phantom.Relay.App.Pictures;
using Phantom.Relay.App.Stats;
using Phantom.Relay.Profiles;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;

namespace Phantom.Relay.App;

/// <summary>
/// The stream on screen. Every frame it gathers the frame's input into pad 0 (<see cref="IPadComposer"/>) and sends it
/// the moment it changes, then draws the newest decoded picture letterboxed at 16:9: where the device opens textures
/// the decoder's device shares, straight from the decoder's GPU (<see cref="GpuPicturePresenter"/>, no copy through
/// the CPU), else read back and uploaded through the engine's picture renderer (YCbCr to RGB on the GPU either way).
/// Each new picture's time from decode to present and each frame's CPU time for its picture go to the stats
/// (<see cref="PresentMeter"/>). It plays under the running game's profile, switching when the console names another
/// title; a keyboard and mouse profile locks the mouse to the window while it has the focus (F10 frees or takes it).
/// F3 shows the stats: as a small block over the window's top-left corner (<see cref="StatsOverlay"/>), short in the
/// window's title and in full in the log, each rebuilt only when a new snapshot arrives. F4 turns frame generation off
/// or on again and F5 steps through its settings (<see cref="FrameGenerationSwitch"/>), live: the decode thread makes the
/// new stage before its next new picture, so the render thread never waits; the stats show what runs, and with the
/// stats hidden the title shows what was chosen. A window larger than the picture shows it upscaled
/// (<see cref="StreamPicture"/>: FSR 1 unless the profile says otherwise); F6 steps through FSR 1, off and xBR, live.
/// Fullscreen (F11, Alt+Enter) waits for the engine: its window has no fullscreen call yet. Escape closes the window, except while keyboard and mouse play, when it
/// is the game's and Shift+Escape closes it; closing it stops the stream, and with it the session on the console.
/// </summary>
public sealed class StreamApplication : IApplication
{
    private const byte FirstPad = 0;

    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private readonly IGraphicsDevice _device;
    private readonly IWindow _window;
    private readonly StreamConnection _stream;
    private readonly IPadComposer _pads;
    private readonly PadReportPolicy _reports;
    private readonly StreamPicture _picture;
    private readonly TimeProvider _clock;
    private readonly PresentMeter _meter = new();
    private readonly StatsOverlay _overlay;
    private readonly FrameGenerationSwitch _generation = new();
    private readonly ILogger<StreamApplication> _log;

    private TimeSpan _now;
    private bool _showStats;
    private bool _upscalerChanged;
    private StreamStats? _shownStats;
    private StreamProfile? _profile;
    private bool _captureMouse;

    public StreamApplication(IGraphicsDevice device, IWindow window, StreamConnection stream, IPadComposer pads, PadReportPolicy reports, ISystemFonts fonts, TimeProvider clock, ILogger<StreamApplication> log)
    {
        _log = log;
        _clock = clock;

        _device = device;
        _window = window;
        _stream = stream;
        _pads = pads;
        _reports = reports;

        // Pictures read back, and those left on the GPU where the decoder can share them; upscaled to a larger window.
        _picture = new StreamPicture(device, stream);
        _overlay = new StatsOverlay(device, window, fonts, log);
    }

    public void Initialize()
    {
        _picture.Initialize();
        _overlay.Initialize();
        ShowTitle();
    }

    public void Frame(double seconds, InputFrame input)
    {
        FollowProfile();
        HandleKeys(input);
        CaptureMouse(input);
        SendPad(input, seconds);
        Present();
        ShowTitle();
    }

    public void DeviceLost()
    {
    }

    public void DeviceRestored()
    {
    }

    /// <summary>The window closes: the stream ends cleanly, the session stopped on the console.</summary>
    public void Cleanup()
    {
        _picture.Cleanup();
        _overlay.Cleanup();
        _stream.Stop();
    }

    /// <summary>
    /// The newest picture, letterboxed to 16:9 (upscaled into it when the window is larger), then the commit; the GPU
    /// path's access ends after it. The CPU time of the picture's own work (taking it, its upscaler's passes, uploading
    /// or opening it, drawing it, ending its access) is measured without the window pass's start and the commit, which
    /// wait for the display; a new picture's time since its decode with them.
    /// </summary>
    private void Present()
    {
        long started = _clock.GetTimestamp();
        var destination = PictureLetterbox.Of(_window.Width, _window.Height);
        _picture.Take();
        _picture.Prepare(destination, _window.Width, _window.Height);
        long taken = _clock.GetTimestamp();
        _device.BeginFramePass(Black);

        long begun = _clock.GetTimestamp();
        _picture.Draw(destination);
        long drawn = _clock.GetTimestamp();
        DrawStats();
        _device.EndPass();
        _device.Commit();

        long committed = _clock.GetTimestamp();
        _picture.EndFrame();
        _meter.Frame(_clock.GetElapsedTime(started, taken) + _clock.GetElapsedTime(begun, drawn) + _clock.GetElapsedTime(committed));

        if (_picture.Fresh)
        {
            _meter.Presented(_clock.GetElapsedTime(_picture.ReadyAt, committed), _picture.OnGpu);
        }
    }

    /// <summary>The stats' block over the picture while F3 shows them (outside the picture's CPU time).</summary>
    [HotPath]
    private void DrawStats()
    {
        if (_showStats)
        {
            _overlay.Draw();
        }
    }

    private void HandleKeys(InputFrame input)
    {
        foreach (var inputEvent in input.Events)
        {
            if (inputEvent.Kind != InputEventKind.KeyDown || inputEvent.Repeat)
            {
                continue;
            }

            if (inputEvent.Key == Key.Escape)
            {
                QuitUnlessPlayed(input);
            }
            else if (inputEvent.Key == Key.F3)
            {
                _showStats = !_showStats;
                _shownStats = null;
                _overlay.Clear();
                _picture.TimePasses(_showStats);
            }
            else if (inputEvent.Key is Key.F4 or Key.F5)
            {
                SwitchGeneration(inputEvent.Key);
            }
            else if (inputEvent.Key == Key.F6)
            {
                SwitchUpscaler();
            }
            else if (inputEvent.Key == Key.F10 && KeyboardMousePlays())
            {
                _captureMouse = !_captureMouse;
            }
        }
    }

    /// <summary>
    /// Escape ends the stream, except while keyboard and mouse play: then it is the game's (its pause menu), and
    /// Shift+Escape ends the stream.
    /// </summary>
    private void QuitUnlessPlayed(InputFrame input)
    {
        bool shifted = input.IsKeyDown(Key.LeftShift) || input.IsKeyDown(Key.RightShift);

        if (shifted || !KeyboardMousePlays())
        {
            _window.Quit();
        }
    }

    /// <summary>A new title's profile plays from this frame on (its pad, its frame generation); a keyboard and mouse profile takes the mouse.</summary>
    private void FollowProfile()
    {
        var profile = _stream.Profile;

        if (ReferenceEquals(profile, _profile))
        {
            return;
        }

        _profile = profile;
        _pads.Use(profile);
        _stream.GpuPictures?.Use(_generation.Follow(profile.FrameGeneration));
        _picture.Use(profile.Upscaling.Method);
        _reports.Reset();
        _captureMouse = KeyboardMousePlays();
    }

    /// <summary>F4 turns frame generation off or on again, F5 picks its next setting; the title shows the choice at once.</summary>
    private void SwitchGeneration(Key key)
    {
        FrameGeneration settings;

        if (key == Key.F4)
        {
            settings = _generation.Toggle();
        }
        else
        {
            settings = _generation.Next();
        }

        _stream.GpuPictures?.Use(settings);
        _shownStats = null;

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.GenerationChosen(_generation.Describe());
        }
    }

    /// <summary>F6 picks the next upscaler (FSR 1, off, xBR); the title shows it at once.</summary>
    private void SwitchUpscaler()
    {
        _picture.Use(StreamUpscaler.Next(_picture.Upscaler));
        _upscalerChanged = true;
        _shownStats = null;
    }

    /// <summary>The mouse is locked to the window while it is captured and the window has the focus (F10 toggles it).</summary>
    private void CaptureMouse(InputFrame input)
    {
        bool lockIt = _captureMouse && input.Focused;

        if (lockIt != _window.MouseLocked)
        {
            _window.LockMouse(lockIt);
        }
    }

    private bool KeyboardMousePlays()
    {
        return _profile is not null && _profile.KeyboardMouse.Enabled;
    }

    /// <summary>A change goes out at once; an unchanged pad is repeated at the policy's keep-alive; focus loss rests it.</summary>
    private void SendPad(InputFrame input, double seconds)
    {
        _now += TimeSpan.FromSeconds(seconds);
        var pad = _pads.Compose(input, (float)seconds);

        if (_reports.ShouldSend(in pad, input.Focused, _now, out var report))
        {
            _stream.SendPad(FirstPad, in report);
        }
    }

    /// <summary>How the pictures come: the source's own account, or why the device takes none on the GPU.</summary>
    private GpuPictureReport Report()
    {
        var report = _stream.GpuPictures?.Report ?? GpuPictureReport.Idle;
        var refusal = _picture.Refusal;

        if (refusal is not null)
        {
            report = report with { Path = $"{GpuPictureReport.ReadBack}: {refusal}" };
        }

        return report;
    }

    /// <summary>The title changes only with a new snapshot (once a second at most) or the F3 toggle.</summary>
    private void ShowTitle()
    {
        var stats = _stream.Stats;

        if (ReferenceEquals(stats, _shownStats))
        {
            return;
        }

        _shownStats = stats;
        var present = _meter.Take();

        if (!_showStats)
        {
            _window.SetTitle(QuietTitle(stats));

            return;
        }

        var report = Report();
        var upscale = _picture.Describe();
        _window.SetTitle(StatsText.Title(stats, present, report, upscale));
        _overlay.Show(StatsText.Lines(stats, present, report, _pads.Source, upscale));

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.Stats(StatsText.WithStats(stats, present, report, upscale));
        }
    }

    /// <summary>The title with the stats hidden: the plain one, and what F4, F5 or F6 chose once they were used.</summary>
    private string QuietTitle(StreamStats stats)
    {
        var title = StatsText.Plain(stats);

        if (_generation.Changed)
        {
            title = $"{title} | frame generation {_generation.Describe()}";
        }

        if (_upscalerChanged)
        {
            title = $"{title} | {_picture.Describe()}";
        }

        return StatsText.Clamp(title);
    }
}
