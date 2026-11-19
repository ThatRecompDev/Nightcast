// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.Versioning;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Windows.Devices;
using Phantom.Relay.Pads.Windows.Hid;
using Phantom.Relay.Pads.Windows.Reports;
using Xunit.Abstractions;

namespace Phantom.Relay.Pads.Windows.Tests.Hid;

/// <summary>
/// What the real system holds: lists the PlayStation pads plugged in or paired, reads a few reports from each, sets
/// its light bar and prints what it saw. Passes with no pad present.
/// </summary>
public sealed class LivePadTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Live")]
    public void Lists_and_reads_the_pads_present()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var host = new WindowsHidHost();
        var paths = new List<string>();
        host.Enumerate(paths);
        output.WriteLine($"{paths.Count} HID interfaces");

        foreach (var path in paths)
        {
            if (host.TryIdentify(path, out var vendor, out var product) && PlayStationProducts.TryModel(vendor, product, out var model))
            {
                ReadPad(host, path, model);
            }
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public void The_source_finds_the_pads_on_its_threads_and_stops_them()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var pads = new GamepadState[4];
        using var source = new PlayStationPads();
        source.Start();
        Thread.Sleep(500);
        source.Poll(pads);

        // A short, light rumble, then still again.
        source.TrySetRumble(0, 0.2f, 0.2f);
        Thread.Sleep(200);
        source.TrySetRumble(0, 0f, 0f);
        Thread.Sleep(50);
        source.Stop();

        output.WriteLine($"slot 0 {pads[0]}, family {source.Family(0)}");
    }

    [SupportedOSPlatform("windows")]
    private void ReadPad(WindowsHidHost host, string path, PadModel model)
    {
        var device = host.Open(path);

        if (device is null)
        {
            output.WriteLine($"{model}: present but held by another program");

            return;
        }

        var slot = new PadSlot();
        var reader = new PadReader(device, model, slot);
        slot.Assign(reader);
        reader.Begin();
        output.WriteLine($"{model} over {reader.Transport}: input {device.InputReportLength}, output {device.OutputReportLength}, feature {device.FeatureReportLength}");

        for (var step = 0; step < 50; step++)
        {
            reader.Step();
        }

        // The light bar to a light blue, written straight to the pad to see whether it takes the report.
        var report = new byte[PadOutputReports.MaxLength];
        var length = PadOutputReports.Write(report, model, reader.Transport, new PadOutput(0, 0, true, 0, 64, 255, true), 0);
        var written = device.Write(report.AsSpan(0, length));

        slot.TryMotion(out var motion);
        output.WriteLine($"state {slot.State()}");
        output.WriteLine($"motion {motion}; light written {written}");
        reader.Finish();
    }
}
