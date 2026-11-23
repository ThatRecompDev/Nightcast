// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Linux.Devices;

/// <summary>What a read of an event device came back with.</summary>
internal enum EvdevReadResult
{
    /// <summary>One or more events arrived.</summary>
    Events,

    /// <summary>No event yet: the wait ran out or <see cref="IEvdevDevice.Wake"/> ended it early.</summary>
    Idle,

    /// <summary>The device is gone (unplugged, switched off, Steam's virtual pad removed) or failed: it is read no more.</summary>
    Gone,
}
