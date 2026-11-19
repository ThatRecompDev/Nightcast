// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Numerics;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Media.Rendering;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The probe's window: every display refresh it takes the newest decoded picture and draws it letterboxed at 16:9 with
/// the engine's picture renderer (YCbCr to RGB on the GPU). The title shows the pictures drawn per second. Escape
/// closes it.
/// </summary>
public sealed class ViewerApplication : IApplication
{
    private const float StreamAspect = 16f / 9f;
    private const string Title = "Phantom.Relay";
    private const float Half = 0.5f;

    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly TimeSpan TitleInterval = TimeSpan.FromSeconds(1);

    private readonly IGraphicsDevice _device;
    private readonly IWindow _window;
    private readonly PictureMailbox _mailbox;
    private readonly MovieRenderer _renderer;

    private double _sinceTitle;
    private int _newPictures;

    public ViewerApplication(IGraphicsDevice device, IWindow window, PictureMailbox mailbox)
    {
        _device = device;
        _window = window;
        _mailbox = mailbox;
        _renderer = new MovieRenderer(device);
    }

    public void Initialize()
    {
        _renderer.Initialize(PassTarget.Frame, BlendMode.None);
        _window.SetTitle($"{Title}: waiting for the console");
    }

    public void Frame(double seconds, InputFrame input)
    {
        Quit(input);

        if (_mailbox.TryTake())
        {
            _newPictures++;
        }

        // Draw the newest picture, letterboxed to 16:9.
        _device.BeginFramePass(Black);

        if (_mailbox.HasPicture)
        {
            _renderer.Upload(_mailbox.Front);
            _renderer.Draw(Letterbox());
        }

        _device.EndPass();
        _device.Commit();
        ShowRate(seconds);
    }

    public void DeviceLost()
    {
    }

    public void DeviceRestored()
    {
    }

    public void Cleanup()
    {
        _renderer.Cleanup();
    }

    /// <summary>The 16:9 rectangle centred in the window, as fractions of it (x, y, width, height).</summary>
    private Vector4 Letterbox()
    {
        float aspect = (float)_window.Width / Math.Max(1, _window.Height);

        if (aspect > StreamAspect)
        {
            float width = StreamAspect / aspect;

            return new Vector4((1f - width) * Half, 0f, width, 1f);
        }

        float height = aspect / StreamAspect;

        return new Vector4(0f, (1f - height) * Half, 1f, height);
    }

    private void ShowRate(double seconds)
    {
        _sinceTitle += seconds;

        if (_sinceTitle < TitleInterval.TotalSeconds || !_mailbox.HasPicture)
        {
            return;
        }

        int rate = (int)Math.Round(_newPictures / _sinceTitle);
        _window.SetTitle(string.Create(CultureInfo.InvariantCulture, $"{Title}: {_mailbox.Front.Width}x{_mailbox.Front.Height}, {rate} fps"));
        _sinceTitle = 0;
        _newPictures = 0;
    }

    private void Quit(InputFrame input)
    {
        foreach (var inputEvent in input.Events)
        {
            if (inputEvent.Kind == InputEventKind.KeyDown && inputEvent.Key == Key.Escape)
            {
                _window.Quit();
            }
        }
    }
}
