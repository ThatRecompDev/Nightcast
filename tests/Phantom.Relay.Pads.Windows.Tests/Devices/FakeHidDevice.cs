// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Windows.Devices;

namespace Phantom.Relay.Pads.Windows.Tests.Devices;

/// <summary>A pad without hardware: the reports a test queues, the feature reads and writes it saw, never a wait.</summary>
internal sealed class FakeHidDevice : IHidDevice
{
    private readonly Queue<byte[]> _reports = new();

    public FakeHidDevice(int inputReportLength)
    {
        InputReportLength = inputReportLength;
    }

    public int InputReportLength { get; }

    public int OutputReportLength => 78;

    public int FeatureReportLength => 64;

    /// <summary>Set to make every read from now on find the pad gone.</summary>
    public bool Unplugged { get; set; }

    public bool Disposed { get; private set; }

    public int Wakes { get; private set; }

    public List<byte> FeatureReads { get; } = [];

    public List<byte[]> Writes { get; } = [];

    public void Queue(byte[] report)
    {
        _reports.Enqueue(report);
    }

    public HidReadResult Read(Span<byte> report, out int length)
    {
        length = 0;

        if (Unplugged)
        {
            return HidReadResult.Gone;
        }

        if (!_reports.TryDequeue(out var next))
        {
            return HidReadResult.Idle;
        }

        length = Math.Min(next.Length, report.Length);
        next.AsSpan(0, length).CopyTo(report);

        return HidReadResult.Report;
    }

    public void Wake()
    {
        Wakes++;
    }

    public bool ReadFeature(Span<byte> report)
    {
        FeatureReads.Add(report[0]);

        return true;
    }

    public bool Write(ReadOnlySpan<byte> report)
    {
        Writes.Add(report.ToArray());

        return true;
    }

    public void Dispose()
    {
        Disposed = true;
    }
}
