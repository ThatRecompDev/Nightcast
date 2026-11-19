// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Relay.Stream.Channels;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Sdp;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// One live probe (docs/architecture/probe.md): sign in when needed, list the consoles, start a session on the chosen
/// one, wait until it takes an offer, send the probe offer (with the bitrate feedback switched on) and show what the
/// console answers, and always stop the session it started. It never prints a token.
/// </summary>
public sealed class ProbeRun
{
    public const int Succeeded = 0;
    public const int NoConsole = 2;
    public const int Refused = 3;

    private readonly TextSetup _setup;
    private readonly IXHomeSessionService _sessions;
    private readonly ILiveIce _ice;
    private readonly DtlsCertificate _certificate;
    private readonly IRandomSource _random;
    private readonly TextWriter _output;

    public ProbeRun(IXboxAuthService auth, IXHomeSessionService sessions, ILiveIce ice, DtlsCertificate certificate, IRandomSource random, TextReader input, TextWriter output)
    {
        _setup = new TextSetup(auth, sessions, input, output);
        _sessions = sessions;
        _ice = ice;
        _certificate = certificate;

        // The run's randomness and its console out.
        _random = random;
        _output = output;
    }

    public Task<int> RunAsync(StreamSettings settings, CancellationToken cancellation)
    {
        return RunAsync(settings, BitrateFeedback.Off, cancellation);
    }

    /// <summary>The run, offering the bitrate <paramref name="feedback"/> (the live ICE run must send the same).</summary>
    public async Task<int> RunAsync(StreamSettings settings, BitrateFeedback feedback, CancellationToken cancellation)
    {
        try
        {
            await _setup.SignInAsync(cancellation);
            var console = await _setup.ChooseConsoleAsync(cancellation);

            if (console is null)
            {
                return NoConsole;
            }

            await ProbeConsoleAsync(console, settings, feedback, cancellation);

            return Succeeded;
        }
        catch (XboxAuthException error)
        {
            _output.WriteLine($"Sign-in refused: {error.Message}");

            return Refused;
        }
        catch (XHomeSessionException error)
        {
            _output.WriteLine($"Streaming service refused: {error.Message}");

            return Refused;
        }
    }

    private async Task ProbeConsoleAsync(XboxConsole console, StreamSettings settings, BitrateFeedback feedback, CancellationToken cancellation)
    {
        _output.WriteLine($"Starting a session on {console.Name}, asking for {settings.Resolution} (OS name '{settings.OsName}')");
        var session = await _sessions.StartAsync(console.ServerId, settings, cancellation);

        try
        {
            await _sessions.WaitUntilReadyAsync(session, new StateWriter(_output), cancellation);
            await OfferAsync(session, feedback, cancellation);
        }
        finally
        {
            await StopAsync(session);
        }
    }

    private async Task OfferAsync(StreamSession session, BitrateFeedback feedback, CancellationToken cancellation)
    {
        var ice = IceCredentials.Create(_random);
        _output.WriteLine($"Sending the offer (bitrate feedback: {feedback.Describe()}); the console answers:");
        string offer = StreamOffer.Create(ice, _certificate.Fingerprint, _random, feedback);
        string answer = await _sessions.ExchangeSdpAsync(session, offer, cancellation);

        foreach (string line in AnswerSummary.Of(answer))
        {
            _output.WriteLine($"  {line}");
        }

        _output.WriteLine($"  the answer kept of the bitrate feedback: {AnswerFeedback.Of(answer).Describe()}");

        _output.WriteLine("Running ICE against the console:");
        var outcome = await _ice.RunAsync(session, ice, _certificate, answer, cancellation);
        ReportIce(outcome);
    }

    private void ReportIce(IceOutcome outcome)
    {
        _output.WriteLine($"  ICE: {outcome.State} after {Milliseconds(outcome.Elapsed)} ({outcome.RemoteCandidates} console candidates)");

        if (outcome.Selected is not null)
        {
            _output.WriteLine($"  selected path: {outcome.Selected}");
            _output.WriteLine($"  round trip to the console: first {Milliseconds(outcome.FirstRoundTrip)}, keep-alive {Milliseconds(outcome.LatestRoundTrip)}");
        }

        _output.WriteLine($"  DTLS: {outcome.DtlsResult} after {Milliseconds(outcome.DtlsElapsed)}");

        if (outcome.SrtpProfile is not null)
        {
            _output.WriteLine($"  cipher suite 0x{outcome.CipherSuite:X4}, SRTP profile {outcome.SrtpProfile}");
        }

        _output.WriteLine($"  received after ICE: {outcome.DtlsDatagrams} DTLS, {outcome.RtpDatagrams} RTP/RTCP, {outcome.OtherDatagrams} other datagrams");
        ReportChannels(outcome.Channels);
        ReportMedia(outcome.Media);
    }

    private void ReportMedia(MediaReport? media)
    {
        if (media is null)
        {
            return;
        }

        var counts = media.Counts;
        _output.WriteLine($"  SRTP: {counts.Decrypted} decrypted, {counts.Refused} refused; {counts.Rtcp} RTCP, {counts.Audio} sound packets");
        _output.WriteLine($"  video: {counts.AccessUnits} frames assembled ({counts.Keyframes} keyframes), {counts.Dropped} dropped, {counts.Lost} packets lost, {counts.KeyframeRequests} keyframe requests");
        ReportBitrate(media);

        if (media.DecoderProblem is not null)
        {
            _output.WriteLine($"  decoder: stopped ({media.DecoderProblem})");

            return;
        }

        _output.WriteLine($"  decoded: {media.Pictures} pictures at {media.Width}x{media.Height}; decode {Milliseconds(media.AverageDecode)} average, {Milliseconds(media.WorstDecode)} worst; decode queue at most {media.DeepestQueue} deep");
    }

    /// <summary>The bitrate over the seconds that carried video, and the feedback that went out.</summary>
    private void ReportBitrate(MediaReport media)
    {
        var counts = media.Counts;
        long unit = BitrateTally.Average(counts.VideoBytes, counts.AccessUnits);
        _output.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"  bitrate: video {media.VideoAverageMegabits:F1} Mbit/s average, {media.VideoPeakMegabits:F1} peak; access unit {unit} bytes on average; sound {media.AudioAverageMegabits:F2} Mbit/s"));
        _output.WriteLine($"  feedback sent: {counts.RembSent} REMB, {counts.TransportFeedbackSent} transport feedback");
    }

    private void ReportChannels(ChannelReport? channels)
    {
        if (channels is null)
        {
            return;
        }

        _output.WriteLine($"  SCTP: {channels.Association}; channels open: {string.Join(", ", channels.Opened)}");
        _output.WriteLine($"  messages: {channels.MessagesSent} sent, {channels.MessagesReceived} received");

        foreach (string kind in channels.ConsoleKinds)
        {
            _output.WriteLine($"    console -> {kind}");
        }

        _output.WriteLine($"  first video/audio packet: {Milliseconds(channels.FirstRtpAfter)} after the channels started");
    }

    private static string Milliseconds(TimeSpan? span)
    {
        if (span is null)
        {
            return "not measured";
        }

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{span.Value.TotalMilliseconds:F2} ms");
    }

    /// <summary>Stops the session even when the run was cancelled, so the console is never left waiting.</summary>
    private async Task StopAsync(StreamSession session)
    {
        try
        {
            await _sessions.StopAsync(session, CancellationToken.None);
            _output.WriteLine("Session stopped.");
        }
        catch (XHomeSessionException error)
        {
            _output.WriteLine($"Stopping the session failed: {error.Message}");
        }
    }
}
