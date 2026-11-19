// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Media.Rendering;
using Phantom.Relay.App.Input;
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
/// F3 shows the stats in the window's title, rebuilt only when a new snapshot arrives. Escape closes the window, except
/// while keyboard and mouse play, when it is the game's and Shift+Escape closes it; closing it stops the stream, and
/// with it the session on the console.
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
    private readonly MovieRenderer _renderer;
    private readonly GpuPicturePresenter? _gpu;
    private readonly TimeProvider _clock;
    private readonly PresentMeter _meter = new();

    private TimeSpan _now;
    private bool _onGpu;
    private bool _fresh;
    private bool _showStats;
    private StreamStats? _shownStats;
    private StreamProfile? _profile;
    private bool _captureMouse;

    public StreamApplication(IGraphicsDevice device, IWindow window, StreamConnection stream, IPadComposer pads, PadReportPolicy reports, TimeProvider clock)
    {
        _device = device;
        _window = window;
        _stream = stream;
        _pads = pads;
        _reports = reports;
        _clock = clock;

        // Pictures read back, and those left on the GPU where the decoder can share them.
        _renderer = new MovieRenderer(device);
        var shared = stream.GpuPictures;

        if (shared is not null)
        {
            _gpu = new GpuPicturePresenter(device, shared);
        }
    }

    public void Initialize()
    {
        _renderer.Initialize(PassTarget.Frame, BlendMode.None);
        _gpu?.Initialize();
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
        _gpu?.Cleanup();
        _renderer.Cleanup();
        _stream.Stop();
    }

    /// <summary>
    /// The newest picture, letterboxed to 16:9, then the commit; the GPU path's access ends after it. The CPU time of
    /// the picture's own work (taking it, uploading or opening it, drawing it, ending its access) is measured without
    /// the pass's start and the commit, which wait for the display; a new picture's time since its decode with them.
    /// </summary>
    private void Present()
    {
        long started = _clock.GetTimestamp();
        TakePicture();
        long taken = _clock.GetTimestamp();
        _device.BeginFramePass(Black);

        long begun = _clock.GetTimestamp();
        DrawPicture(PictureLetterbox.Of(_window.Width, _window.Height));
        long drawn = _clock.GetTimestamp();
        _device.EndPass();
        _device.Commit();

        long committed = _clock.GetTimestamp();
        _gpu?.EndFrame();
        _meter.Frame(_clock.GetElapsedTime(started, taken) + _clock.GetElapsedTime(begun, drawn) + _clock.GetElapsedTime(committed));

        if (_fresh)
        {
            _meter.Presented(_clock.GetElapsedTime(ReadyAt(), committed), _onGpu);
        }
    }

    /// <summary>A new picture from either path; the GPU path's wins when both have one (the switch to it).</summary>
    private void TakePicture()
    {
        _fresh = false;

        if (_stream.Pictures.TryTake())
        {
            _onGpu = false;
            _fresh = true;
        }

        if (_gpu is not null && _gpu.TryTake())
        {
            _onGpu = true;
            _fresh = true;
        }
    }

    private void DrawPicture(Vector4 destination)
    {
        if (_onGpu && _gpu is not null)
        {
            _gpu.Draw(destination);

            return;
        }

        if (_stream.Pictures.HasPicture)
        {
            _renderer.Upload(_stream.Pictures.Front);
            _renderer.Draw(destination);
        }
    }

    private long ReadyAt()
    {
        if (_onGpu && _gpu is not null)
        {
            return _gpu.Current.DecodedAt;
        }

        return _stream.Pictures.FrontReadyAt;
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

    /// <summary>A new title's profile plays from this frame on; a keyboard and mouse profile takes the mouse.</summary>
    private void FollowProfile()
    {
        var profile = _stream.Profile;

        if (ReferenceEquals(profile, _profile))
        {
            return;
        }

        _profile = profile;
        _pads.Use(profile);
        _reports.Reset();
        _captureMouse = KeyboardMousePlays();
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
        var refusal = _gpu?.Refusal;

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

        if (_showStats)
        {
            _window.SetTitle(StatsText.WithStats(stats, present, Report()));
        }
        else
        {
            _window.SetTitle(StatsText.Plain(stats));
        }
    }
}
