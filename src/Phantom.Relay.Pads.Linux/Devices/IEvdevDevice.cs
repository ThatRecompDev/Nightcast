// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Devices;

/// <summary>
/// One open event device, used by one reader thread only (<see cref="Wake"/> excepted, which any thread may call
/// while the device is open).
/// </summary>
internal interface IEvdevDevice : IDisposable
{
    /// <summary>
    /// Waits for events, at most a short while, and copies as many whole <c>input_event</c>s as there are and fit into
    /// <paramref name="events"/>; a <see cref="Wake"/> ends the wait early with <see cref="EvdevReadResult.Idle"/>.
    /// </summary>
    EvdevReadResult Read(Span<byte> events, out int length);

    /// <summary>Ends a <see cref="Read"/> waiting now, or the next one, so the reader can write or stop.</summary>
    void Wake();

    /// <summary>
    /// Feeds <paramref name="pad"/> the device's current keys and axes, as events: after it was opened and after the
    /// kernel dropped events (<c>SYN_DROPPED</c>), so no button stays held that was let go meanwhile.
    /// </summary>
    void Resynchronise(EvdevPadState pad);

    /// <summary>
    /// Runs the motors at <paramref name="strong"/> (the low-frequency one) and <paramref name="weak"/> (the high) from 0
    /// to 65535 until set again, 0 and 0 stopping them; false when the device has no rumble or the write failed.
    /// </summary>
    bool Rumble(ushort strong, ushort weak);
}
