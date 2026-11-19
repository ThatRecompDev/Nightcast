// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe;

/// <summary>Runs ICE against the console for one session, after the SDP exchange.</summary>
public interface ILiveIce
{
    Task<IceOutcome> RunAsync(StreamSession session, IceCredentials local, string answer, CancellationToken cancellation);
}
