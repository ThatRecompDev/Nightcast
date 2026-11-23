// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.App.Setup;

namespace Phantom.Relay.App.Tests.Setup;

/// <summary>An application that reads each frame's input as the setup screen's keys and keeps the last.</summary>
public sealed class ReadingApplication : IApplication
{
    private readonly SetupInput _input = new();

    public SetupKeys Keys { get; private set; }

    public void Initialize()
    {
    }

    public void Frame(double seconds, InputFrame input)
    {
        Keys = _input.Read(input);
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
