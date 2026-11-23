// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Linux.Devices;

/// <summary>
/// Runs the pads' readers: a thread each on the desktop (<see cref="ThreadPadWorkers"/>), stepped by hand in a test.
/// </summary>
internal interface IPadWorkers
{
    /// <summary>Runs <paramref name="reader"/> (<see cref="EvdevReader.Run"/>) until it stops or its pad is gone.</summary>
    void Start(EvdevReader reader);

    /// <summary>Waits for every reader started to end; each was told to stop first.</summary>
    void Join();
}
