// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Input;
using Phantom.Relay.Stream.Link;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// ICE, then DTLS, against the real console, measured: Stream's <see cref="ConsoleLink"/> does the work, and this loop
/// pumps it phase by phase with a deadline each, then listens a little longer and reports what the console sent, with
/// the bitrate once a second on the <c>progress</c> writer. Our host candidate goes through the service's ICE exchange.
/// </summary>
public sealed class LiveIce : ILiveIce
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(1);

    private readonly IXHomeSessionService _sessions;
    private readonly IDatagramTransport _transport;
    private readonly uint _hostAddress;
    private readonly DecodeWorker _video;
    private readonly SilentAudioSink _audio = new();
    private readonly PadMailbox _pads = new();
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly TimeSpan _listen;
    private readonly BitrateFeedback _feedback;
    private readonly TextWriter _progress;

    /// <summary>
    /// Keeps the stream for <c>listen</c> once connected: a few seconds to measure, <see cref="TimeSpan.MaxValue"/> to
    /// watch until cancelled. The media path sends the bitrate <c>feedback</c> the offer proposed.
    /// </summary>
    public LiveIce(IXHomeSessionService sessions, IDatagramTransport transport, uint hostAddress, DecodeWorker video, TimeProvider clock, IRandomSource random, TimeSpan listen, BitrateFeedback feedback, TextWriter progress)
    {
        _sessions = sessions;
        _transport = transport;
        _hostAddress = hostAddress;
        _video = video;

        // The run's clock, randomness and length.
        _clock = clock;
        _random = random;
        _listen = listen;
        _feedback = feedback;
        _progress = progress;
    }

    public async Task<IceOutcome> RunAsync(StreamSession session, IceCredentials local, DtlsCertificate certificate, string answer, CancellationToken cancellation)
    {
        if (SessionIce.ReadCredentials(answer) is null)
        {
            return new IceOutcome(IceAgentState.Failed, null, TimeSpan.Zero, null, null, 0, 0, 0, 0);
        }

        using var link = new ConsoleLink(_transport, local, _video, _audio, _pads, _clock, _random, _feedback);
        var exchanged = await _sessions.ExchangeIceAsync(session, [link.HostCandidate(_hostAddress)], cancellation);
        long start = _clock.GetTimestamp();
        link.StartIce(answer, exchanged);

        Pump(link, Deadline, () => link.Ice.State != IceAgentState.Checking, cancellation);
        var ice = link.Ice;
        var outcome = new IceOutcome(ice.State, ice.Selected, _clock.GetElapsedTime(start), ice.RoundTrip, null, link.RemoteCandidates, 0, 0, 0);

        if (ice.State == IceAgentState.Connected)
        {
            outcome = RunDtls(link, certificate, outcome, cancellation);
            var tally = new BitrateTally(_clock);
            Listen(link, tally, cancellation);
            outcome = outcome with { Channels = link.Channels?.Report(link.FirstRtpAfter), Media = MediaOf(link, tally) };
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

    private MediaReport? MediaOf(ConsoleLink link, BitrateTally tally)
    {
        var media = link.Media;

        if (media is null)
        {
            return null;
        }

        return new MediaReport(media.Counts(), _video.Pictures, _video.Width, _video.Height, _video.AverageDecode(), _video.WorstDecode(), _video.DeepestQueue, _video.Problem)
        {
            VideoAverageMegabits = tally.AverageVideo,
            VideoPeakMegabits = tally.PeakVideo,
            AudioAverageMegabits = tally.AverageAudio,
        };
    }

    /// <summary>Pumps for the listening time as <see cref="Pump"/> does, writing the bitrate once a second.</summary>
    private void Listen(ConsoleLink link, BitrateTally tally, CancellationToken cancellation)
    {
        long start = _clock.GetTimestamp();
        long reportedAt = start;
        Report(link, tally);

        while (_clock.GetElapsedTime(start) < _listen)
        {
            cancellation.ThrowIfCancellationRequested();
            link.Pump();

            if (_clock.GetElapsedTime(reportedAt) >= ReportInterval)
            {
                Report(link, tally);
                reportedAt = _clock.GetTimestamp();
            }

            Thread.Yield();
        }
    }

    private void Report(ConsoleLink link, BitrateTally tally)
    {
        var media = link.Media;
        string? line = null;

        if (media is not null)
        {
            line = tally.Sample(media.Counts());
        }

        if (line is not null)
        {
            _progress.WriteLine(line);
        }
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
