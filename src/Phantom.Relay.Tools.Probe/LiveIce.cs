// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.Stream.Link;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// ICE, then DTLS, against the real console, measured: Stream's <see cref="ConsoleLink"/> does the work, and this loop
/// pumps it phase by phase with a deadline each, then listens a little longer and reports what the console sent. Our
/// host candidate goes through the service's ICE exchange.
/// </summary>
public sealed class LiveIce : ILiveIce
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private readonly IXHomeSessionService _sessions;
    private readonly IDatagramTransport _transport;
    private readonly uint _hostAddress;
    private readonly DecodeWorker _video;
    private readonly SilentAudioSink _audio = new();
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly TimeSpan _listen;

    /// <summary>
    /// Keeps the stream for <c>listen</c> once connected: a few seconds to measure, <see cref="TimeSpan.MaxValue"/> to
    /// watch until cancelled.
    /// </summary>
    public LiveIce(IXHomeSessionService sessions, IDatagramTransport transport, uint hostAddress, DecodeWorker video, TimeProvider clock, IRandomSource random, TimeSpan listen)
    {
        _sessions = sessions;
        _transport = transport;
        _hostAddress = hostAddress;
        _video = video;

        // The run's clock, randomness and length.
        _clock = clock;
        _random = random;
        _listen = listen;
    }

    public async Task<IceOutcome> RunAsync(StreamSession session, IceCredentials local, DtlsCertificate certificate, string answer, CancellationToken cancellation)
    {
        if (SessionIce.ReadCredentials(answer) is null)
        {
            return new IceOutcome(IceAgentState.Failed, null, TimeSpan.Zero, null, null, 0, 0, 0, 0);
        }

        using var link = new ConsoleLink(_transport, local, _video, _audio, _clock, _random);
        var exchanged = await _sessions.ExchangeIceAsync(session, [link.HostCandidate(_hostAddress)], cancellation);
        long start = _clock.GetTimestamp();
        link.StartIce(answer, exchanged);

        Pump(link, Deadline, () => link.Ice.State != IceAgentState.Checking, cancellation);
        var ice = link.Ice;
        var outcome = new IceOutcome(ice.State, ice.Selected, _clock.GetElapsedTime(start), ice.RoundTrip, null, link.RemoteCandidates, 0, 0, 0);

        if (ice.State == IceAgentState.Connected)
        {
            outcome = RunDtls(link, certificate, outcome, cancellation);
            Pump(link, _listen, static () => false, cancellation);
            outcome = outcome with { Channels = link.Channels?.Report(link.FirstRtpAfter), Media = MediaOf(link) };
        }

        return outcome with { LatestRoundTrip = ice.RoundTrip, DtlsDatagrams = link.DtlsDatagrams, RtpDatagrams = link.RtpDatagrams, OtherDatagrams = link.OtherDatagrams };
    }

    /// <summary>Runs the handshake on its own thread while this loop carries its datagrams over the selected path.</summary>
    private IceOutcome RunDtls(ConsoleLink link, DtlsCertificate certificate, IceOutcome outcome, CancellationToken cancellation)
    {
        if (!link.StartDtls(certificate))
        {
            return outcome with { DtlsResult = "the answer carries no fingerprint" };
        }

        long start = _clock.GetTimestamp();
        string? failure = null;
        bool finished = false;
        Pump(link, Deadline, () => finished = link.TryFinishDtls(out failure), cancellation);

        if (!finished)
        {
            return outcome with { DtlsResult = "timed out", DtlsElapsed = _clock.GetElapsedTime(start) };
        }

        if (failure is not null)
        {
            return outcome with { DtlsResult = $"failed: {failure}", DtlsElapsed = link.DtlsElapsed };
        }

        return outcome with { DtlsResult = "connected", DtlsElapsed = link.DtlsElapsed, SrtpProfile = link.SrtpProfile?.ToString(), CipherSuite = link.CipherSuite };
    }

    private MediaReport? MediaOf(ConsoleLink link)
    {
        var media = link.Media;

        if (media is null)
        {
            return null;
        }

        return new MediaReport(media.Counts(), _video.Pictures, _video.Width, _video.Height, _video.AverageDecode(), _video.WorstDecode(), _video.DeepestQueue, _video.Problem);
    }

    /// <summary>
    /// Pumps without sleeping: a timed sleep on Windows lasts a whole ~15.6 ms timer tick. Yielding the thread reacts
    /// within microseconds, at the cost of one busy core while the probe runs. The engine's wait-for-datagram call on
    /// the transport port will replace the yield.
    /// </summary>
    private void Pump(ConsoleLink link, TimeSpan length, Func<bool> done, CancellationToken cancellation)
    {
        long start = _clock.GetTimestamp();

        while (_clock.GetElapsedTime(start) < length && !done())
        {
            cancellation.ThrowIfCancellationRequested();
            link.Pump();
            Thread.Yield();
        }
    }
}
