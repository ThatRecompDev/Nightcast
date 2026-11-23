// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Devices;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Tests.Devices;

/// <summary>The system's event devices as a test plugs and unplugs them.</summary>
internal sealed class FakeEvdevHost : IEvdevHost
{
    private readonly Dictionary<string, (EvdevDescription? Description, FakeEvdevDevice Device)> _devices = [];

    public Dictionary<string, int> Descriptions { get; } = [];

    /// <summary>Paths that cannot be opened now (no permission yet).</summary>
    public HashSet<string> Busy { get; } = [];

    public FakeEvdevDevice Plug(string path, EvdevDescription? description)
    {
        var device = new FakeEvdevDevice();
        _devices[path] = (description, device);

        return device;
    }

    public void Unplug(string path)
    {
        _devices[path].Device.Unplugged = true;
        _devices.Remove(path);
    }

    public void Enumerate(List<string> paths)
    {
        paths.AddRange(_devices.Keys);
    }

    public bool TryDescribe(string path, out bool gamepad, out EvdevDescription? description)
    {
        Descriptions[path] = Descriptions.GetValueOrDefault(path) + 1;
        gamepad = false;
        description = null;

        if (!_devices.TryGetValue(path, out var device))
        {
            return false;
        }

        description = device.Description;
        gamepad = description is not null;

        return true;
    }

    public IEvdevDevice? Open(string path, EvdevDescription description)
    {
        if (Busy.Contains(path) || !_devices.TryGetValue(path, out var device))
        {
            return null;
        }

        return device.Device;
    }
}
