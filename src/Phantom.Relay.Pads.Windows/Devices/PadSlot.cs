// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Relay.Pads.Windows.Reports;

namespace Phantom.Relay.Pads.Windows.Devices;

/// <summary>
/// One gamepad slot, shared between the frame and the slot's reader thread under a lock held for a copy only: the
/// newest state and motion the reader read, and the feedback the game asked for that the reader has still to write.
/// The frame never waits on the pad: the reader writes outside the lock.
/// </summary>
internal sealed class PadSlot
{
    private readonly Lock _gate = new();
    private PadReader? _reader;
    private GamepadState _state;
    private PadMotion _motion;
    private PadOutput _output;
    private bool _outputPending;

    /// <summary>The newest state; the default (not connected) while no pad has reported.</summary>
    [HotPath]
    public GamepadState State()
    {
        lock (_gate)
        {
            return _state;
        }
    }

    [HotPath]
    public GamepadFamily Family()
    {
        lock (_gate)
        {
            if (!_state.Connected)
            {
                return GamepadFamily.Unknown;
            }

            return GamepadFamily.PlayStation;
        }
    }

    /// <summary>The newest motion; false while no pad has reported.</summary>
    [HotPath]
    public bool TryMotion(out PadMotion motion)
    {
        lock (_gate)
        {
            motion = _motion;

            return _state.Connected;
        }
    }

    /// <summary>A pad was opened for this slot: its reader takes it, with nothing to write yet.</summary>
    public void Assign(PadReader reader)
    {
        lock (_gate)
        {
            _reader = reader;
            _state = default;
            _motion = default;
            _output = default;
            _outputPending = false;
        }
    }

    /// <summary>The reader's newest report.</summary>
    public void Publish(in GamepadState state, in PadMotion motion)
    {
        lock (_gate)
        {
            _state = state;
            _motion = motion;
        }
    }

    /// <summary>The reader ended: the slot is empty again.</summary>
    public void Release(PadReader reader)
    {
        lock (_gate)
        {
            if (_reader != reader)
            {
                return;
            }

            _reader = null;
            _state = default;
            _motion = default;
            _outputPending = false;
        }
    }

    [HotPath]
    public bool TrySetRumble(byte low, byte high)
    {
        PadReader? reader;

        lock (_gate)
        {
            if (!_state.Connected)
            {
                return false;
            }

            _output = _output with { LowMotor = low, HighMotor = high };
            _outputPending = true;
            reader = _reader;
        }

        reader?.Wake();

        return true;
    }

    [HotPath]
    public bool TrySetLight(byte red, byte green, byte blue)
    {
        PadReader? reader;

        lock (_gate)
        {
            if (!_state.Connected)
            {
                return false;
            }

            _output = _output with { Light = true, Red = red, Green = green, Blue = blue };
            _outputPending = true;
            reader = _reader;
        }

        reader?.Wake();

        return true;
    }

    /// <summary>The feedback to write, once: false when nothing changed since the last take.</summary>
    public bool TryTakeOutput(out PadOutput output)
    {
        lock (_gate)
        {
            output = _output;
            var pending = _outputPending;
            _outputPending = false;

            return pending;
        }
    }
}
