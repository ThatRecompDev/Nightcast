// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Pads.Linux.Interop;

/// <summary><c>struct pollfd</c>: a descriptor, the events waited for, and those that happened.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PollDescriptor
{
    public int Descriptor;
    public short Events;
    public short ReturnedEvents;
}
