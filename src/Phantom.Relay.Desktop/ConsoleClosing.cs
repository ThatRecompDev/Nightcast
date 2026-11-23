// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Stream;

namespace Phantom.Relay.Desktop;

/// <summary>
/// Closing the console window (or Windows signing out or shutting down) ends the process within a few seconds, without
/// the window's own clean-up: the stream is stopped first, from the signal, so the console's pads say goodbye and the
/// session ends on the service instead of lingering on the console.
/// </summary>
public sealed class ConsoleClosing : IDisposable
{
    private readonly PosixSignalRegistration _closed;
    private readonly PosixSignalRegistration _terminated;

    public ConsoleClosing(StreamConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // SIGHUP is the console window closing, SIGTERM a sign-out or shutdown.
        _closed = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => connection.Stop());
        _terminated = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => connection.Stop());
    }

    public void Dispose()
    {
        _closed.Dispose();
        _terminated.Dispose();
    }
}
