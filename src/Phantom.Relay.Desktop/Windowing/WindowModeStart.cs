// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.App;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>
/// The window takes the head's mode (fullscreen) as soon as it exists, on the first screen already (the sign-in), not
/// only once the stream starts; otherwise the application's own.
/// </summary>
internal sealed class WindowModeStart : IApplication
{
    private readonly IApplication _application;
    private readonly IWindowMode _mode;

    public WindowModeStart(IApplication application, IWindowMode mode)
    {
        _application = application;
        _mode = mode;
    }

    public void Initialize()
    {
        _application.Initialize();
        _mode.Begin();
    }

    public void Frame(double seconds, InputFrame input)
    {
        _application.Frame(seconds, input);
    }

    public void DeviceLost()
    {
        _application.DeviceLost();
    }

    public void DeviceRestored()
    {
        _application.DeviceRestored();
    }

    public void Cleanup()
    {
        _application.Cleanup();
    }
}
