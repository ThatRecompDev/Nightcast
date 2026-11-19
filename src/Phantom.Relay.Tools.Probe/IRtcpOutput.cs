// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe;

/// <summary>Where protected RTCP feedback goes: the selected path to the console.</summary>
public interface IRtcpOutput
{
    void Send(ReadOnlySpan<byte> packet);
}
