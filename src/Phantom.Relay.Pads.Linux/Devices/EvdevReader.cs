// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Devices;

/// <summary>
/// Reads one pad on its own thread and writes its rumble there: it takes the device's current state
/// (<see cref="Begin"/>), then reads events into its pad state, publishing it into the slot at each <c>SYN_REPORT</c>
/// and writing the rumble the game asked for between reads (<see cref="Step"/>), until it is stopped or the pad is gone
/// (<see cref="Finish"/>). After a <c>SYN_DROPPED</c> the events up to the next report are dropped, as the kernel's
/// guide says, and the state is read again. Its buffer is made once.
/// </summary>
internal sealed class EvdevReader
{
    /// <summary>NOT DATA: events read at once; a pad sends a handful per report.</summary>
    private const int EventsPerRead = 64;

    private readonly IEvdevDevice _device;
    private readonly EvdevPadState _pad;
    private readonly EvdevSlot _slot;
    private readonly byte[] _events = new byte[EventsPerRead * InputEvent.Size];
    private readonly Lock _deviceGate = new();
    private bool _dropping;
    private bool _closed;
    private volatile bool _stopping;
    private volatile bool _finished;

    public EvdevReader(IEvdevDevice device, EvdevPadState pad, EvdevSlot slot)
    {
        _device = device;
        _pad = pad;
        _slot = slot;
    }

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

    /// <summary>Reads the device's current keys and axes and publishes them: the pad is in its slot from the start.</summary>
    public void Begin()
    {
        _device.Resynchronise(_pad);
        _slot.Publish(_pad.State());
    }

    /// <summary>Reads the next events into the slot and writes pending rumble; false once the pad is gone.</summary>
    public bool Step()
    {
        var result = _device.Read(_events, out var length);

        if (result == EvdevReadResult.Gone)
        {
            return false;
        }

        if (result == EvdevReadResult.Events)
        {
            Take(_events.AsSpan(0, length));
        }

        WriteRumble();

        return true;
    }

    /// <summary>Asks the reader to end; its current read ends at once.</summary>
    public void Stop()
    {
        _stopping = true;
        Wake();
    }

    /// <summary>Ends a read waiting now, so pending rumble is written without waiting for the next event.</summary>
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

    /// <summary>Stops the motors, empties the slot and closes the pad. The reader's thread calls it last.</summary>
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

    private void Take(ReadOnlySpan<byte> events)
    {
        for (var offset = 0; offset + InputEvent.Size <= events.Length; offset += InputEvent.Size)
        {
            var input = InputEvent.Read(events[offset..]);

            if (input.Type != EvdevCodes.Synchronize)
            {
                Apply(input);
            }
            else if (input.Code == EvdevCodes.Dropped)
            {
                _dropping = true;
            }
            else if (input.Code == EvdevCodes.Report)
            {
                Report();
            }
        }
    }

    private void Apply(InputEvent input)
    {
        if (!_dropping)
        {
            _pad.Apply(input);
        }
    }

    /// <summary>One whole state: published, or, after a drop, read again from the device first.</summary>
    private void Report()
    {
        if (_dropping)
        {
            _dropping = false;
            _device.Resynchronise(_pad);
        }

        _slot.Publish(_pad.State());
    }

    private void WriteRumble()
    {
        if (_slot.TryTakeRumble(out var strong, out var weak))
        {
            _device.Rumble(strong, weak);
        }
    }
}
