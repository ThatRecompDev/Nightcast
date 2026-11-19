// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Stream.Input;

/// <summary>
/// The pads' newest states between the thread that reads the pads and the stream's network thread. Each pad keeps only
/// its newest state not yet sent: a state posted twice before the network thread looks is sent once, as the newer, and
/// a state posted after one was taken is taken again, so no change is ever lost. Safe from any thread; nothing
/// allocates.
/// </summary>
public sealed class PadMailbox
{
    /// <summary>The pads the console takes at once.</summary>
    public const int MaxPads = 4;

    private readonly Lock _gate = new();
    private readonly XboxPadState[] _states = new XboxPadState[MaxPads];
    private readonly bool[] _pending = new bool[MaxPads];

    /// <summary>The pad's newest state, replacing one that was not taken yet.</summary>
    [HotPath]
    public void Post(byte index, in XboxPadState pad)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, (byte)MaxPads);

        lock (_gate)
        {
            _states[index] = pad;
            _pending[index] = true;
        }
    }

    /// <summary>The next pad with a state not yet taken, lowest index first, and that state; false when none waits.</summary>
    [HotPath]
    public bool TryTake(out byte index, out XboxPadState pad)
    {
        lock (_gate)
        {
            for (int i = 0; i < MaxPads; i++)
            {
                if (!_pending[i])
                {
                    continue;
                }

                _pending[i] = false;
                index = (byte)i;
                pad = _states[i];

                return true;
            }
        }

        index = 0;
        pad = default;

        return false;
    }

    /// <summary>A taken state that could not be sent goes back, unless a newer one was posted meanwhile.</summary>
    [HotPath]
    public void Restore(byte index, in XboxPadState pad)
    {
        lock (_gate)
        {
            if (_pending[index])
            {
                return;
            }

            _states[index] = pad;
            _pending[index] = true;
        }
    }
}
