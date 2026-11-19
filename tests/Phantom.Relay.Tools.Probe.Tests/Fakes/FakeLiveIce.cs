// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe.Tests.Fakes;

/// <summary>Records the credentials, certificate and answer it was given and returns a connected outcome.</summary>
public sealed class FakeLiveIce : ILiveIce
{
    public IceCredentials? Local { get; private set; }

    public string? Answer { get; private set; }

    public DtlsCertificate? Certificate { get; private set; }

    public Task<IceOutcome> RunAsync(StreamSession session, IceCredentials local, DtlsCertificate certificate, string answer, CancellationToken cancellation)
    {
        Local = local;
        Answer = answer;
        Certificate = certificate;
        var selected = DatagramEndpoint.FromOctets(192, 168, 1, 20, 9002);
        var roundTrip = TimeSpan.FromMilliseconds(1.25);
        var outcome = new IceOutcome(IceAgentState.Connected, selected, TimeSpan.FromMilliseconds(42), roundTrip, roundTrip, 2, 3, 0, 0);

        return Task.FromResult(outcome with { DtlsResult = "connected", DtlsElapsed = TimeSpan.FromMilliseconds(8), SrtpProfile = "AeadAes128Gcm", CipherSuite = 0xC02B });
    }
}
