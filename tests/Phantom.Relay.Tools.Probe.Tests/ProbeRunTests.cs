// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Relay.Tools.Probe.Tests.Fakes;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe.Tests;

public sealed class ProbeRunTests : IDisposable
{
    private static readonly StreamSettings Settings = new(StreamResolution.P1440, "en-US", 60);

    private static readonly DtlsCertificate Certificate =
        DtlsCertificate.Create(new Org.BouncyCastle.Security.SecureRandom(), new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    private readonly FakeAuth _auth = new() { IsSignedIn = true };
    private readonly FakeSessions _sessions = new();
    private readonly FakeLiveIce _ice = new();
    private readonly StringWriter _output = new();

    [Fact]
    public async Task A_signed_in_run_starts_on_the_only_console_shows_the_answer_and_stops()
    {
        _sessions.Consoles.Add(new XboxConsole("F4001", "Living room", "XboxSeriesX", "On", true));

        int exit = await Run(string.Empty);

        Assert.Equal(ProbeRun.Succeeded, exit);
        Assert.Equal("F4001", _sessions.StartedOn);
        Assert.Equal(StreamResolution.P1440, _sessions.StartedWith!.Resolution);
        Assert.StartsWith("v=0", _sessions.Offer, StringComparison.Ordinal);
        Assert.Equal(1, _sessions.Stops);

        Assert.Contains("  a=rtpmap:102 H264/90000", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains("session: Provisioned", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ice_runs_with_the_credentials_the_offer_carried_and_its_outcome_is_shown()
    {
        _sessions.Consoles.Add(new XboxConsole("F4001", "Living room", "XboxSeriesX", "On", true));

        await Run(string.Empty);

        Assert.Contains($"a=ice-ufrag:{_ice.Local!.Ufrag}", _sessions.Offer, StringComparison.Ordinal);
        Assert.Contains($"a=ice-pwd:{_ice.Local.Password}", _sessions.Offer, StringComparison.Ordinal);
        Assert.Equal(_sessions.Answer, _ice.Answer);
        Assert.Contains("ICE: Connected after 42.00 ms (2 console candidates)", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains("received after ICE: 3 DTLS", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains("round trip to the console: first 1.25 ms", _output.ToString(), StringComparison.Ordinal);

        Assert.Contains($"a=fingerprint:sha-256 {Certificate.Fingerprint}", _sessions.Offer, StringComparison.Ordinal);
        Assert.Same(Certificate, _ice.Certificate);
        Assert.Contains("DTLS: connected after 8.00 ms", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains("cipher suite 0xC02B, SRTP profile AeadAes128Gcm", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Signing_in_reads_the_pasted_address()
    {
        _auth.IsSignedIn = false;
        _sessions.Consoles.Add(new XboxConsole("F4001", "Living room", "XboxSeriesX", "On", true));

        await Run("https://login.live.com/oauth20_desktop.srf?code=abc\n");

        Assert.Equal(new Uri("https://login.live.com/oauth20_desktop.srf?code=abc"), _auth.Redirected);
        Assert.Contains("https://login.example.test/authorize", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_offer_still_stops_the_session()
    {
        _sessions.Consoles.Add(new XboxConsole("F4001", "Living room", "XboxSeriesX", "On", true));
        _sessions.RefuseOffer = true;

        int exit = await Run(string.Empty);

        Assert.Equal(ProbeRun.Refused, exit);
        Assert.Equal(1, _sessions.Stops);
        Assert.Contains("refused the offer", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_several_consoles_the_chosen_number_is_used()
    {
        _sessions.Consoles.Add(new XboxConsole("F4001", "Living room", "XboxSeriesX", "On", true));
        _sessions.Consoles.Add(new XboxConsole("F4002", "Bedroom", "XboxSeriesS", "ConnectedStandby", true));

        await Run("2\n");

        Assert.Equal("F4002", _sessions.StartedOn);
    }

    [Fact]
    public async Task Without_a_console_nothing_is_started()
    {
        int exit = await Run(string.Empty);

        Assert.Equal(ProbeRun.NoConsole, exit);
        Assert.Null(_sessions.StartedOn);
        Assert.Contains("Remote features", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_token_is_ever_written()
    {
        _sessions.Consoles.Add(new XboxConsole("F4001", "Living room", "XboxSeriesX", "On", true));

        await Run(string.Empty);

        Assert.DoesNotContain("gs-token", _output.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _output.Dispose();
    }

    private Task<int> Run(string input)
    {
        var run = new ProbeRun(_auth, _sessions, _ice, Certificate, new SeededRandomSource(1), new StringReader(input), _output);

        return run.RunAsync(Settings, CancellationToken.None);
    }
}
