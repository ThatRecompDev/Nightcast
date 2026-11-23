// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App.Logging;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.App.Setup;

/// <summary>
/// The window from its first frame: the setup on screen (<see cref="SetupSession"/>: the sign-in code, the consoles to
/// pick from, what went wrong), large and centred (<see cref="SetupText"/>), then the stream in the same window once a
/// console is chosen: the head's <c>startStream</c> starts the stream on it and gives the stream's application, which
/// is initialised then and gets every frame from that one on. A setup that failed stays on screen until B or Escape,
/// which close the window; B or Escape while it signs in closes it too.
/// </summary>
public sealed class SetupApplication : IApplication
{
    private static readonly Vector4 Background = new(0.05f, 0.05f, 0.07f, 1f);

    private readonly IGraphicsDevice _device;
    private readonly IWindow _window;
    private readonly SetupSession _session;
    private readonly Func<XboxConsole, IApplication> _startStream;
    private readonly ILogger _log;
    private readonly SetupInput _input = new();
    private readonly SetupText _text;
    private IApplication? _stream;
    private int _shownVersion = -1;
    private int _shownWidth;
    private int _shownHeight;

    /// <summary><paramref name="startStream"/> starts the stream on the console chosen and gives its application.</summary>
    public SetupApplication(IGraphicsDevice device, IWindow window, ISystemFonts fonts, SetupSession session, Func<XboxConsole, IApplication> startStream, ILogger log)
    {
        _device = device;
        _window = window;
        _session = session;
        _startStream = startStream;
        _log = log;
        _text = new SetupText(device, window, fonts, log);
    }

    /// <summary>True once a console was chosen and the stream took the window over.</summary>
    public bool HandedOver => _stream is not null;

    /// <summary>Where the setup ended (or is): <see cref="SetupPhase.Failed"/> when it closed on a failure shown.</summary>
    public SetupPhase Phase => _session.Phase;

    public void Initialize()
    {
        _text.Initialize();
        _window.SetTitle(RelayApp.Name);
        _session.Start();
    }

    public void Frame(double seconds, InputFrame input)
    {
        if (_stream is not null)
        {
            _stream.Frame(seconds, input);

            return;
        }

        var phase = _session.Step(_input.Read(input));

        if (phase == SetupPhase.Chosen)
        {
            HandOver();
            _stream!.Frame(seconds, input);

            return;
        }

        if (phase == SetupPhase.Closed)
        {
            _window.Quit();
        }

        Present();
    }

    public void DeviceLost()
    {
        _stream?.DeviceLost();
    }

    public void DeviceRestored()
    {
        _stream?.DeviceRestored();
    }

    public void Cleanup()
    {
        if (_stream is not null)
        {
            _stream.Cleanup();
        }
        else
        {
            _text.Cleanup();
        }

        _session.Dispose();
    }

    /// <summary>The setup's text goes; the stream starts on the console chosen and its application takes the window.</summary>
    private void HandOver()
    {
        _text.Cleanup();
        _stream = _startStream(_session.Console!);
        _stream.Initialize();
        _log.SetupHandedOver();
    }

    /// <summary>The lines, laid out again when they or the window changed.</summary>
    private void Present()
    {
        if (_session.Version != _shownVersion || _window.Width != _shownWidth || _window.Height != _shownHeight)
        {
            _shownVersion = _session.Version;
            _shownWidth = _window.Width;
            _shownHeight = _window.Height;
            _text.Show(_session.Lines());
        }

        _device.BeginFramePass(Background);
        _text.Draw();
        _device.EndPass();
        _device.Commit();
    }
}
