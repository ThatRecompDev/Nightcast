// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.App.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Tests.Input;

/// <summary>An application that composes each frame's pad and keeps the last one.</summary>
public sealed class ComposingApplication : IApplication
{
    private readonly IPadComposer _composer;

    public ComposingApplication(IPadComposer composer)
    {
        _composer = composer;
    }

    public XboxPadState Composed { get; private set; }

    public void Initialize()
    {
    }

    public void Frame(double seconds, InputFrame input)
    {
        Composed = _composer.Compose(input);
    }

    public void DeviceLost()
    {
    }

    public void DeviceRestored()
    {
    }

    public void Cleanup()
    {
    }
}
