// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Tests.Fakes;
using Phantom.Relay.Xbox.Input;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Tests;

/// <summary>
/// The connection's states up to the point where a real console is needed: the service's refusals, the answer's
/// checks, ICE without a path, and a stop while the console provisions. ICE, DTLS and the media are checked live by
/// the probe.
/// </summary>
public sealed class StreamConnectionTests : IDisposable
{
    /// <summary>192.168.1.20, this machine on the console's network.</summary>
    private const uint HostAddress = 0xC0A80114;

    private static readonly TimeSpan Ends = TimeSpan.FromSeconds(10);
    private static readonly StreamSettings Settings = new(StreamResolution.P1440, "en-US", 60);

    private readonly FakeSessions _sessions = new();
    private readonly SilentTransport _transport = new();
    private readonly FakeDecoder _decoder = new();
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly StreamConnection _connection;

    public StreamConnectionTests()
    {
        _connection = new StreamConnection(
            _sessions,
            _transport,
            HostAddress,
            _decoder,
            new SilentAudioSink(),
            [StreamProfile.Default],
            _clock,
            new SeededRandomSource(5),
            NullLogger<StreamConnection>.Instance);
    }

    [Fact]
    public void Before_it_starts_it_is_idle_with_the_default_profile()
    {
        Assert.Equal(StreamState.Idle, _connection.State);
        Assert.Same(StreamProfile.Default, _connection.Profile);
        Assert.Null(_connection.TitleId);
        Assert.False(_connection.Pictures.HasPicture);
    }

    [Fact]
    public void A_refused_session_fails_with_the_services_reason_and_has_nothing_to_stop()
    {
        _sessions.RefuseStart = true;

        _connection.Start("F4001", Settings);

        Assert.True(_connection.Join(Ends));
        Assert.Equal(StreamState.Failed, _connection.State);
        Assert.Contains("refused the session", _connection.Stats.FailureReason, StringComparison.Ordinal);
        Assert.Equal(0, _sessions.Stops);
    }

    [Fact]
    public void The_offer_carries_fresh_ice_credentials_and_the_certificate_fingerprint()
    {
        _connection.Start("F4001", Settings);

        Assert.True(_connection.Join(Ends));
        Assert.Equal("F4001", _sessions.StartedOn);
        Assert.StartsWith("v=0\r\n", _sessions.Offer, StringComparison.Ordinal);
        Assert.Contains("a=ice-ufrag:", _sessions.Offer, StringComparison.Ordinal);
        Assert.Matches("a=fingerprint:sha-256 ([0-9A-F]{2}:){31}[0-9A-F]{2}\r\n", _sessions.Offer);
    }

    [Fact]
    public void An_answer_without_ice_credentials_fails_and_the_session_is_stopped()
    {
        _connection.Start("F4001", Settings);

        Assert.True(_connection.Join(Ends));
        Assert.Equal(StreamState.Failed, _connection.State);
        Assert.Equal("the console's answer carries no ICE credentials", _connection.Stats.FailureReason);
        Assert.Equal(1, _sessions.Stops);
    }

    [Fact]
    public void Ice_without_a_candidate_fails_after_our_host_candidate_was_sent_and_the_session_is_stopped()
    {
        _sessions.Answer = "v=0\r\na=ice-ufrag:c0n5\r\na=ice-pwd:0123456789abcdef0123456789abcdef\r\nm=video 9 UDP/TLS/RTP/SAVPF 104\r\n";

        _connection.Start("F4001", Settings);

        Assert.True(_connection.Join(Ends));
        Assert.Equal(StreamState.Failed, _connection.State);
        Assert.Equal("ICE found no path to the console", _connection.Stats.FailureReason);
        Assert.Contains("192.168.1.20 50000 typ host", Assert.Single(_sessions.SentCandidates!).Candidate, StringComparison.Ordinal);
        Assert.Equal(1, _sessions.Stops);
    }

    [Fact]
    public void Stopping_while_the_console_provisions_ends_cleanly_and_stops_the_session()
    {
        _sessions.WaitUntilStopped = true;
        _connection.Start("F4001", Settings);
        Assert.True(_sessions.Waiting.Wait(Ends));

        _connection.Stop();

        Assert.Equal(StreamState.Stopped, _connection.State);
        Assert.Null(_connection.Stats.FailureReason);
        Assert.Equal(1, _sessions.Stops);
    }

    [Fact]
    public void A_connection_streams_once()
    {
        _sessions.RefuseStart = true;
        _connection.Start("F4001", Settings);

        Assert.Throws<InvalidOperationException>(() => _connection.Start("F4001", Settings));
    }

    [Fact]
    public void Pads_can_be_sent_from_any_thread_before_the_input_channel_opens()
    {
        var pad = new XboxPadState(XboxButtons.A, 0f, 1f, 0f, 0f, 0f, 0f);

        _connection.SendPad(0, pad);
        _connection.SendPad(3, pad);

        Assert.Throws<ArgumentOutOfRangeException>(() => _connection.SendPad(4, pad));
    }

    [Fact]
    public void The_decoder_stays_the_heads_to_dispose()
    {
        _sessions.RefuseStart = true;
        _connection.Start("F4001", Settings);

        _connection.Dispose();

        Assert.False(_decoder.Disposed);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _sessions.Dispose();
    }
}
