// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;

namespace Phantom.Relay.Pads.Linux.Devices;

/// <summary>
/// One gamepad slot, shared between the frame and the slot's reader thread under a lock held for a copy only: the
/// newest state the reader built, the pad's family, and the rumble the game asked for that the reader has still to
/// write. The frame never waits on the pad: the reader writes outside the lock.
/// </summary>
internal sealed class EvdevSlot
{
    private readonly Lock _gate = new();
    private EvdevReader? _reader;
    private GamepadState _state;
    private GamepadFamily _family;
    private ushort _strong;
    private ushort _weak;
    private bool _rumblePending;

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

            return _family;
        }
    }

    /// <summary>A pad was opened for this slot: its reader takes it, with nothing to write yet.</summary>
    public void Assign(EvdevReader reader, GamepadFamily family)
    {
        lock (_gate)
        {
            _reader = reader;
            _family = family;
            _state = default;
            _strong = 0;
            _weak = 0;
            _rumblePending = false;
        }
    }

    /// <summary>The reader's newest state.</summary>
    public void Publish(in GamepadState state)
    {
        lock (_gate)
        {
            _state = state;
        }
    }

    /// <summary>The reader ended: the slot is empty again.</summary>
    public void Release(EvdevReader reader)
    {
        lock (_gate)
        {
            if (_reader != reader)
            {
                return;
            }

            _reader = null;
            _state = default;
            _rumblePending = false;
        }
    }

    [HotPath]
    public bool TrySetRumble(ushort strong, ushort weak)
    {
        EvdevReader? reader;

        lock (_gate)
        {
            if ((_state.Features & GamepadFeatures.Rumble) == 0)
            {
                return false;
            }

            // The same strengths again (a game sets them every frame) are already playing or about to.
            if (strong == _strong && weak == _weak)
            {
                return true;
            }

            _strong = strong;
            _weak = weak;
            _rumblePending = true;
            reader = _reader;
        }

        reader?.Wake();

        return true;
    }

    /// <summary>The rumble to write, once: false when nothing changed since the last take.</summary>
    public bool TryTakeRumble(out ushort strong, out ushort weak)
    {
        lock (_gate)
        {
            strong = _strong;
            weak = _weak;
            var pending = _rumblePending;
            _rumblePending = false;

            return pending;
        }
    }
}
