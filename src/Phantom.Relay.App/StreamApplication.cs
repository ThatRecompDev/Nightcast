// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App.Input;
using Phantom.Relay.App.Logging;
using Phantom.Relay.App.Pictures;
using Phantom.Relay.App.Recording;
using Phantom.Relay.App.Stats;
using Phantom.Relay.Profiles;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Media.Gpu;
using Phantom.Relay.Xbox.Input;

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
/// window's title and in full in the log, each rebuilt only when a new snapshot arrives. F4 cycles frame generation through
/// off, LSFG and NVIDIA FRUC, F5 steps through LSFG's settings and F8 the multiplier (x2, x3, x4: 60, 90, 120 fps from a
/// 30 fps game; <see cref="FrameGenerationSwitch"/>), live: the decode thread makes the
/// new stage before its next new picture, so the render thread never waits; the stats show what runs, and with the
/// stats hidden the title shows what was chosen. A window larger than the picture shows it upscaled
/// (<see cref="StreamPicture"/>: FSR 1 unless the profile says otherwise); F6 steps through FSR 1, Lossless Scaling's
/// LS1, its performance setting and SGSR (on the decoder's GPU), xBR and off, live; F7 steps FSR 1's sharpness, or
/// with LS1 its network. F9 starts or stops a recording (<see cref="IStreamRecorder"/>, the head's); while it records
/// and for a few seconds after, its status ("● REC 00:42", where it was saved, the comparison's progress) shows in the
/// title and over the picture's top-left corner (in the stats' block while F3 shows them).
/// F11 or Alt+Enter toggles fullscreen (<see cref="IWindowMode"/>, the head's: the engine's window has no fullscreen
/// call yet), which hides the cursor. Escape closes the window, except while keyboard and mouse play, when it is the
/// game's and Shift+Escape closes it. On the pad alone, View and Menu pressed together open Relay's menu (<see cref="PadMenu"/>:
/// every setting above), and held for two seconds close the window (<see cref="MenuChord"/>). F2 or the menu shows the
/// picture with bars or stretched on a screen of another shape (<see cref="PictureFit"/>); closing it stops the stream, and with it the session on the console. A stream that ends by itself (taken over on
/// the Xbox, failed) closes the window too.
/// </summary>
public sealed class StreamApplication : IApplication
{
    private const byte FirstPad = 0;
    private const float Percent = 100f;

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
    private readonly RumblePlayer _rumble;
    private readonly IStreamRecorder _recorder;
    private readonly StatsOverlay _recordOverlay;
    private readonly IWindowMode _mode;
    private readonly MenuChord _chord = new();
    private readonly PadMenu _menu = new();
    private readonly StatsOverlay _menuOverlay;
    private PictureFit _fit = PictureFit.Bars;
    private XboxPadState _sent;

    private TimeSpan _now;
    private string? _recording;
    private bool _showStats;
    private bool _upscalerChanged;
    private StreamStats? _shownStats;
    private StreamProfile? _profile;
    private bool _captureMouse;

    /// <summary>
    /// The window's app; <c>motors</c> run the console's vibration on the pads, null where the pads have none; F9 goes to
    /// the head's <c>recorder</c>; <c>mode</c> is the window's fullscreen, the head's choice to start in.
    /// </summary>
    public StreamApplication(IGraphicsDevice device, IWindow window, StreamConnection stream, IPadComposer pads, PadReportPolicy reports, ISystemFonts fonts, IGamepadFeedback? motors, IStreamRecorder recorder, IWindowMode mode, TimeProvider clock, ILogger<StreamApplication> log)
    {
        _log = log;
        _clock = clock;
        _rumble = new RumblePlayer(motors);
        _mode = mode;

        _device = device;
        _window = window;
        _stream = stream;
        _pads = pads;
        _reports = reports;
        _recorder = recorder;

        // Pictures read back, and those left on the GPU where the decoder can share them; upscaled to a larger window.
        _picture = new StreamPicture(device, stream);
        _overlay = new StatsOverlay(device, window, fonts, log);
        _recordOverlay = new StatsOverlay(device, window, fonts, log);
        _menuOverlay = new StatsOverlay(device, window, fonts, log);
    }

    public void Initialize()
    {
        _picture.Initialize();
        _overlay.Initialize();
        _recordOverlay.Initialize();
        _menuOverlay.Initialize();
        ShowTitle();

        _mode.Begin();
        ShowCursorWhenWindowed();
    }

    /// <summary>F11 or Alt+Enter: fullscreen to windowed and back; the cursor hides while the window is fullscreen.</summary>
    private void ToggleFullscreen()
    {
        _mode.Toggle();
        ShowCursorWhenWindowed();
    }

    private void ShowCursorWhenWindowed()
    {
        _window.SetCursorVisible(!_mode.Fullscreen);
    }

    public void Frame(double seconds, InputFrame input)
    {
        FollowProfile();
        HandleKeys(input);
        CaptureMouse(input);
        SendPad(input, seconds);
        PlayRumble();
        FollowRecording();

        Present();
        ShowTitle();
        QuitWhenEnded();
    }

    /// <summary>
    /// A stream that ended without the window (the console took it back, another device took it over, the link failed)
    /// closes the window: there is nothing left to show, and the head reports why.
    /// </summary>
    private void QuitWhenEnded()
    {
        var state = _stream.State;

        if (state is StreamState.Failed or StreamState.Stopped)
        {
            _window.Quit();
        }
    }

    /// <summary>The console's vibration since the last frame, on the pad being played; the motors stop on time.</summary>
    private void PlayRumble()
    {
        int slot = _pads.PlayedSlot;

        while (_stream.TryTakeRumble(out var report))
        {
            _rumble.Play(in report, slot, _now);
        }

        _rumble.Tick(slot, _now);
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
        _rumble.Stop();
        _recorder.Stop();

        _picture.Cleanup();
        _overlay.Cleanup();
        _recordOverlay.Cleanup();
        _menuOverlay.Cleanup();
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
        var destination = PictureLetterbox.Of(_window.Width, _window.Height, _fit);
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
        if (_menu.IsOpen)
        {
            _menuOverlay.Draw();
        }
        else if (_showStats)
        {
            _overlay.Draw();
        }
        else if (_recording is not null)
        {
            _recordOverlay.Draw();
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
            else if (inputEvent.Key == Key.F2)
            {
                ToggleFit();
            }
            else if (inputEvent.Key == Key.F3)
            {
                ToggleStats();
            }
            else if (inputEvent.Key is Key.F4 or Key.F5 or Key.F8)
            {
                SwitchGeneration(inputEvent.Key);
            }
            else if (inputEvent.Key == Key.F6)
            {
                SwitchUpscaler();
            }
            else if (inputEvent.Key == Key.F7)
            {
                SwitchSharpness();
            }
            else if (inputEvent.Key == Key.F9)
            {
                ToggleRecording();
            }
            else if (inputEvent.Key == Key.F10 && KeyboardMousePlays())
            {
                _captureMouse = !_captureMouse;
            }
            else if (IsFullscreenKey(input, inputEvent.Key))
            {
                ToggleFullscreen();
            }
        }
    }

    private static bool IsFullscreenKey(InputFrame input, Key key)
    {
        bool alt = input.IsKeyDown(Key.LeftAlt) || input.IsKeyDown(Key.RightAlt);

        return key == Key.F11 || (key == Key.Enter && alt);
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

    /// <summary>F4 cycles frame generation (off, LSFG, NVIDIA FRUC), F5 picks LSFG's next setting, F8 the next multiplier; the title shows the choice at once.</summary>
    private void SwitchGeneration(Key key)
    {
        FrameGeneration settings;

        if (key == Key.F4)
        {
            settings = _generation.Cycle();
        }
        else if (key == Key.F8)
        {
            settings = _generation.NextMultiplier();
        }
        else
        {
            settings = _generation.Next();
        }

        _stream.GpuPictures?.Use(settings);
        _shownStats = null;

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.GenerationChosen(_generation.Describe(0));
        }
    }

    /// <summary>
    /// Relay's pad chord and menu before the pad goes out: View and Menu together open the menu (held, they end the
    /// stream); while the chord is held or the menu is open the console gets the pad at rest.
    /// </summary>
    private XboxPadState FollowPadMenu(in XboxPadState pad)
    {
        var step = _chord.Step(in pad, _now);

        if (step == ChordStep.Quit)
        {
            _window.Quit();
        }
        else if (step == ChordStep.OpenMenu)
        {
            ToggleMenu(pad.Buttons);
        }
        else if (_menu.IsOpen)
        {
            Choose(_menu.Step(pad.Buttons));
        }

        if (step != ChordStep.None || _menu.IsOpen)
        {
            return default;
        }

        return pad;
    }

    private void ToggleMenu(XboxButtons held)
    {
        if (_menu.IsOpen)
        {
            _menu.Close();
        }
        else
        {
            _menu.Open(held);
        }

        ShowMenu();
    }

    /// <summary>A row chosen on the pad does what its key does; the rows are shown again with the new values.</summary>
    private void Choose(MenuItem? item)
    {
        if (item is not null)
        {
            Apply(item.Value);
        }

        if (_menu.Changed)
        {
            ShowMenu();
        }
    }

    private void Apply(MenuItem item)
    {
        switch (item)
        {
            case MenuItem.Stats:
                ToggleStats();
                break;
            case MenuItem.FrameGeneration:
                SwitchGeneration(Key.F4);
                break;
            case MenuItem.LsfgSetting:
                SwitchGeneration(Key.F5);
                break;
            case MenuItem.Multiplier:
                SwitchGeneration(Key.F8);
                break;
            case MenuItem.Upscaler:
                SwitchUpscaler();
                break;
            case MenuItem.Sharpness:
                SwitchSharpness();
                break;
            case MenuItem.PictureFit:
                ToggleFit();
                break;
            case MenuItem.RumbleStrength:
                _rumble.NextGain();
                break;
            case MenuItem.Fullscreen:
                ToggleFullscreen();
                break;
            case MenuItem.Record:
                ToggleRecording();
                break;
            case MenuItem.Close:
                _menu.Close();
                break;
            default:
                _window.Quit();
                break;
        }
    }

    private void ShowMenu()
    {
        if (!_menu.IsOpen)
        {
            _menuOverlay.Clear();

            return;
        }

        _menuOverlay.Show(_menu.Lines(MenuValue));
    }

    /// <summary>What a row shows beside its label: the setting as it is now.</summary>
    private string MenuValue(MenuItem item)
    {
        return item switch
        {
            MenuItem.Stats => OnOff(_showStats),
            MenuItem.FrameGeneration => _generation.Describe(_stream.Stats.NewPicturesPerSecond),
            MenuItem.Upscaler => _picture.Describe(Report().Upscale),
            MenuItem.PictureFit => FitName(_fit),
            MenuItem.RumbleStrength => string.Create(CultureInfo.InvariantCulture, $"{_rumble.Gain * Percent:0} %"),
            MenuItem.Fullscreen => OnOff(_mode.Fullscreen),
            MenuItem.Record => _recording ?? "off",
            _ => string.Empty,
        };
    }

    private static string OnOff(bool on)
    {
        if (on)
        {
            return "on";
        }

        return "off";
    }

    private static string FitName(PictureFit fit)
    {
        if (fit == PictureFit.Stretch)
        {
            return "stretched to the screen";
        }

        return "16:9 with bars";
    }

    /// <summary>F3: the stats block on or off.</summary>
    private void ToggleStats()
    {
        _showStats = !_showStats;
        _shownStats = null;
        _overlay.Clear();
        _picture.TimePasses(_showStats);
    }

    /// <summary>F2: 16:9 with bars, or stretched to a screen of another shape.</summary>
    private void ToggleFit()
    {
        if (_fit == PictureFit.Bars)
        {
            _fit = PictureFit.Stretch;
        }
        else
        {
            _fit = PictureFit.Bars;
        }
    }

    /// <summary>F6 picks the next upscaler (FSR 1, LS1, LS1 performance, SGSR, xBR, off); the title shows it at once.</summary>
    private void SwitchUpscaler()
    {
        _picture.Use(StreamUpscaler.Next(_picture.Upscaler));
        _upscalerChanged = true;
        _shownStats = null;
    }

    /// <summary>
    /// F7 picks FSR 1's next sharpness (RCAS 0.2, 0.1, 0, 0.5 stops), or with LS1 its next network (2, 3, 4, 0, 1); the
    /// title shows it at once.
    /// </summary>
    private void SwitchSharpness()
    {
        if (_picture.Upscaler is UpscaleMethod.Ls1 or UpscaleMethod.Ls1Performance)
        {
            _picture.UseStrength(PictureScale.NextStrength(_picture.Strength));
        }
        else
        {
            _picture.UseSharpness(StreamUpscaler.NextSharpness(_picture.Sharpness));
        }

        _upscalerChanged = true;
        _shownStats = null;
    }

    /// <summary>F9 starts a recording with what the window shows now (the frame generation, its multiplier, the upscaler), or stops it.</summary>
    private void ToggleRecording()
    {
        var report = Report();
        var chosen = _generation.Current;
        var multiplier = chosen.IsOn ? chosen.ClampedMultiplier : 1;
        _recorder.Toggle(new RecordingSettings(report.FrameGeneration, multiplier, _picture.Describe(report.Upscale)));
    }

    /// <summary>The recorder's status, over the picture and in the title, laid out again only when it changed.</summary>
    private void FollowRecording()
    {
        var status = _recorder.Status;

        if (ReferenceEquals(status, _recording))
        {
            return;
        }

        _recording = status;
        _shownStats = null;

        if (status is null)
        {
            _recordOverlay.Clear();

            return;
        }

        _recordOverlay.Show([status]);
    }

    /// <summary>The stats' lines, the recorder's status first while it has one.</summary>
    private string[] WithRecording(string[] lines)
    {
        if (_recording is null)
        {
            return lines;
        }

        return [_recording, .. lines];
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
        var composed = _pads.Compose(input, (float)seconds);
        var pad = FollowPadMenu(in composed);
        _sent = pad;

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
        var upscale = _picture.Describe(report.Upscale);
        _window.SetTitle(StatsText.Clamp(WithRecording(StatsText.Title(stats, present, report, upscale))));
        _overlay.Show(WithRecording([.. StatsText.Lines(stats, present, report, _pads.Source, upscale), SentLine()]));

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.Stats(StatsText.WithStats(stats, present, report, upscale));
        }
    }

    /// <summary>
    /// What goes to the console as pad 0 right now, and from which physical pad: the stick moving here while the game
    /// stands still means the console is not taking the pad, not that the pad is not read.
    /// </summary>
    private string SentLine()
    {
        var pad = _sent;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"sent: {pad.Buttons}, left stick {pad.LeftX:0.00} {pad.LeftY:0.00}, triggers {pad.LeftTrigger:0.00} {pad.RightTrigger:0.00} (pad slot {_pads.PlayedSlot})");
    }

    /// <summary>The title with the stats hidden: the plain one, and what F4 to F7 chose once they were used.</summary>
    private string QuietTitle(StreamStats stats)
    {
        var title = StatsText.Plain(stats);

        if (_generation.Changed)
        {
            title = $"{title} | frame generation {_generation.Describe(stats.NewPicturesPerSecond)}";
        }

        if (_upscalerChanged)
        {
            title = $"{title} | {_picture.Describe(Report().Upscale)}";
        }

        return StatsText.Clamp(WithRecording(title));
    }

    /// <summary>A title with the recorder's status first while it has one.</summary>
    private string WithRecording(string title)
    {
        if (_recording is null)
        {
            return title;
        }

        return $"{_recording} | {title}";
    }
}
