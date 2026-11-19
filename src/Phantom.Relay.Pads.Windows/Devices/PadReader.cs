// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Windows.Reports;

namespace Phantom.Relay.Pads.Windows.Devices;

/// <summary>
/// Reads one pad on its own thread and writes its feedback there: it switches a Bluetooth pad to its full report
/// (<see cref="Begin"/>), then reads report after report into its slot, writing the feedback the game asked for between
/// reads (<see cref="Step"/>), until it is stopped or the pad is gone (<see cref="Finish"/>). Its buffers are made once.
/// </summary>
internal sealed class PadReader
{
    /// <summary>Both pads' USB input report (0x01) is 64 bytes; over Bluetooth the longest input report is longer.</summary>
    private const int UsbInputReportLength = 64;

    private readonly IHidDevice _device;
    private readonly PadModel _model;
    private readonly PadSlot _slot;
    private readonly byte[] _input;
    private readonly byte[] _output = new byte[PadOutputReports.MaxLength];
    private readonly Lock _deviceGate = new();
    private int _sequence;
    private uint _reports;
    private bool _lightReleased;
    private bool _closed;
    private volatile bool _stopping;
    private volatile bool _finished;

    public PadReader(IHidDevice device, PadModel model, PadSlot slot)
    {
        _device = device;
        _model = model;
        _slot = slot;
        _input = new byte[Math.Max(device.InputReportLength, UsbInputReportLength)];
        Transport = TransportOf(device);
    }

    public PadModel Model => _model;

    public PadTransport Transport { get; }

    /// <summary>True once the reader has ended and closed its pad.</summary>
    public bool Finished => _finished;

    /// <summary>The thread's body: <see cref="Begin"/>, <see cref="Step"/> until stopped or gone, <see cref="Finish"/>.</summary>
    public void Run()
    {
        try
        {
            Begin();

            while (!_stopping)
            {
                if (!Step())
                {
                    break;
                }
            }
        }
        finally
        {
            Finish();
        }
    }

    /// <summary>
    /// Over Bluetooth, reads the model's calibration feature report once: a pad sends its short report until a host
    /// reads it, and its full report after.
    /// </summary>
    public void Begin()
    {
        if (Transport != PadTransport.Bluetooth || _device.FeatureReportLength <= 0)
        {
            return;
        }

        var feature = new byte[_device.FeatureReportLength];
        feature[0] = PadInputReports.BluetoothModeFeatureReport(_model);
        _device.ReadFeature(feature);
    }

    /// <summary>Reads the next report into the slot and writes pending feedback; false once the pad is gone.</summary>
    public bool Step()
    {
        var result = _device.Read(_input, out var length);

        if (result == HidReadResult.Gone)
        {
            return false;
        }

        if (result == HidReadResult.Report && PadInputReports.TryRead(_model, Transport, _input.AsSpan(0, length), out var state, out var motion))
        {
            _reports++;
            _slot.Publish(state, motion with { Sequence = _reports });
        }

        WriteOutput();

        return true;
    }

    /// <summary>Asks the reader to end; its current read ends at once.</summary>
    public void Stop()
    {
        _stopping = true;
        Wake();
    }

    /// <summary>Ends a read waiting now, so pending feedback is written without waiting for the next report.</summary>
    public void Wake()
    {
        lock (_deviceGate)
        {
            if (!_closed)
            {
                _device.Wake();
            }
        }
    }

    /// <summary>Empties the slot and closes the pad. The reader's thread calls it last.</summary>
    public void Finish()
    {
        if (_finished)
        {
            return;
        }

        _slot.Release(this);

        lock (_deviceGate)
        {
            _closed = true;
        }

        _device.Dispose();
        _finished = true;
    }

    private void WriteOutput()
    {
        if (!_slot.TryTakeOutput(out var output))
        {
            return;
        }

        var release = output.Light && !_lightReleased;
        var length = PadOutputReports.Write(_output, _model, Transport, output with { ReleaseLight = release }, _sequence);
        _sequence++;

        if (_device.Write(_output.AsSpan(0, length)) && release)
        {
            _lightReleased = true;
        }
    }

    private static PadTransport TransportOf(IHidDevice device)
    {
        if (device.InputReportLength == UsbInputReportLength)
        {
            return PadTransport.Usb;
        }

        return PadTransport.Bluetooth;
    }
}
