// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Devices;

/// <summary>
/// One open HID device, used by one reader thread only (<see cref="Wake"/> excepted, which any thread may call while
/// the device is open). The lengths are the device's own, report id included: the longest report of each kind.
/// </summary>
internal interface IHidDevice : IDisposable
{
    int InputReportLength { get; }

    int OutputReportLength { get; }

    int FeatureReportLength { get; }

    /// <summary>
    /// Waits for the next input report, at most a short while, and copies it into <paramref name="report"/>; a
    /// <see cref="Wake"/> ends the wait early with <see cref="HidReadResult.Idle"/>.
    /// </summary>
    HidReadResult Read(Span<byte> report, out int length);

    /// <summary>Ends a <see cref="Read"/> waiting now, or the next one, so the reader can write or stop.</summary>
    void Wake();

    /// <summary>Reads the feature report whose id is <paramref name="report"/>'s first byte.</summary>
    bool ReadFeature(Span<byte> report);

    /// <summary>Writes an output report (its id first), padded to the device's output length; false when it failed.</summary>
    bool Write(ReadOnlySpan<byte> report);
}
