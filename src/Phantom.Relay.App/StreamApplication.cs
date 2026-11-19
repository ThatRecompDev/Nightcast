// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Media.Rendering;
using Phantom.Relay.App.Input;
using Phantom.Relay.App.Stats;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App;

/// <summary>
/// The stream on screen. Every frame it gathers the frame's input into pad 0 (<see cref="IPadComposer"/>) and sends it
/// the moment it changes, then draws the newest decoded picture letterboxed at 16:9 with the engine's picture renderer
/// (YCbCr to RGB on the GPU). F3 shows the stats in the window's title, rebuilt only when a new snapshot arrives.
/// Escape closes the window; closing it stops the stream, and with it the session on the console.
/// </summary>
public sealed class StreamApplication : IApplication
{
    private const byte FirstPad = 0;

    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private readonly IGraphicsDevice _device;
    private readonly IWindow _window;
    private readonly StreamConnection _stream;
    private readonly IPadComposer _pads;
    private readonly MovieRenderer _renderer;

    private XboxPadState _sentPad;
    private bool _padSent;
    private bool _showStats;
    private StreamStats? _shownStats;

    public StreamApplication(IGraphicsDevice device, IWindow window, StreamConnection stream, IPadComposer pads)
    {
        _device = device;
        _window = window;
        _stream = stream;
        _pads = pads;
        _renderer = new MovieRenderer(device);
    }

    public void Initialize()
    {
        _renderer.Initialize(PassTarget.Frame, BlendMode.None);
        ShowTitle();
    }

    public void Frame(double seconds, InputFrame input)
    {
        HandleKeys(input);
        SendPad(input);

        // Draw the newest picture, letterboxed to 16:9.
        _stream.Pictures.TryTake();
        _device.BeginFramePass(Black);

        if (_stream.Pictures.HasPicture)
        {
            _renderer.Upload(_stream.Pictures.Front);
            _renderer.Draw(PictureLetterbox.Of(_window.Width, _window.Height));
        }

        _device.EndPass();
        _device.Commit();
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
        _renderer.Cleanup();
        _stream.Stop();
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
                _window.Quit();
            }
            else if (inputEvent.Key == Key.F3)
            {
                _showStats = !_showStats;
                _shownStats = null;
            }
        }
    }

    private void SendPad(InputFrame input)
    {
        var pad = _pads.Compose(input);

        if (_padSent && pad == _sentPad)
        {
            return;
        }

        _stream.SendPad(FirstPad, in pad);
        _sentPad = pad;
        _padSent = true;
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

        if (_showStats)
        {
            _window.SetTitle(StatsText.WithStats(stats));
        }
        else
        {
            _window.SetTitle(StatsText.Plain(stats));
        }
    }
}
