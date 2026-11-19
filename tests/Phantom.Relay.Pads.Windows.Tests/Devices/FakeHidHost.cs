// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Windows.Devices;

namespace Phantom.Relay.Pads.Windows.Tests.Devices;

/// <summary>The system's HID devices as a test plugs and unplugs them.</summary>
internal sealed class FakeHidHost : IHidHost
{
    private readonly Dictionary<string, (ushort Vendor, ushort Product, FakeHidDevice Device)> _devices = [];

    public Dictionary<string, int> Identifications { get; } = [];

    /// <summary>Paths another program holds: they are listed and identified but cannot be opened.</summary>
    public HashSet<string> Busy { get; } = [];

    public FakeHidDevice Plug(string path, ushort vendor, ushort product, int inputReportLength)
    {
        var device = new FakeHidDevice(inputReportLength);
        _devices[path] = (vendor, product, device);

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

    public bool TryIdentify(string path, out ushort vendor, out ushort product)
    {
        Identifications[path] = Identifications.GetValueOrDefault(path) + 1;
        vendor = 0;
        product = 0;

        if (!_devices.TryGetValue(path, out var device))
        {
            return false;
        }

        vendor = device.Vendor;
        product = device.Product;

        return true;
    }

    public IHidDevice? Open(string path)
    {
        if (Busy.Contains(path) || !_devices.TryGetValue(path, out var device))
        {
            return null;
        }

        return device.Device;
    }
}
