// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Security;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Media.Video;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Input;
using Phantom.Relay.Stream.Link;
using Phantom.Relay.Stream.Logging;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Network;
using Phantom.Relay.Stream.Sdp;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Input;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream;

/// <summary>
/// One streaming session from start to stop. <see cref="Start"/> starts the session through the streaming service,
/// sends the offer, runs ICE, DTLS, SCTP and the data channels, and receives the media; <see cref="Stop"/> stops the
/// session on the console. All of it runs on one network thread this class owns, which never sleeps on a timer (a
/// Windows timer tick is ~15.6 ms of latency). While streaming it waits for the next datagram where the transport
/// offers that (the engine's <see cref="IDatagramWaiter"/>: woken within microseconds of an arrival), and the injected
/// <see cref="IStreamWaker"/> wakes it for a pad change; during the handshakes, and on a transport that cannot wait,
/// it yields between turns. Video is decoded on a worker thread by the injected decoder into
/// <see cref="Pictures"/>; sound goes to the injected sink from the network thread; pads go out through
/// <see cref="SendPad"/> the moment the network thread sees them. <see cref="Stats"/> is an immutable snapshot any
/// thread reads. An error ends the stream as <see cref="StreamState.Failed"/> with its reason, never as an exception on
/// a thread. One connection streams once.
/// </summary>
public sealed class StreamConnection : IDisposable
{
    private static readonly TimeSpan IceDeadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DtlsDeadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StatsInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The longest wait for a datagram while streaming. A wait that times out lasts a timer tick whatever is asked, so
    /// this only bounds how late the once-a-second work runs when the console falls silent; arrivals and pad changes
    /// end the wait at once.
    /// </summary>
    private static readonly TimeSpan DatagramWait = TimeSpan.FromMilliseconds(5);

    /// <summary>The service ends a session without one (docs/architecture/xhome.md#sessions).</summary>
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    private readonly IXHomeSessionService _sessions;
    private readonly IDatagramTransport _transport;
    private readonly IStreamWaker _waker;
    private readonly uint _hostAddress;
    private readonly IVideoDecoder _decoder;
    private readonly IStreamAudioSink _audio;
    private readonly IReadOnlyList<StreamProfile> _profiles;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly ILogger<StreamConnection> _log;
    private readonly PadMailbox _pads = new();
    private readonly PictureMailbox _pictures = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly StreamStatsMeter _meter;

    private Thread? _thread;
    private StreamStats _stats = StreamStats.Idle;
    private StreamProfile _profile;

    /// <summary>
    /// <c>hostAddress</c> is this machine's IPv4 address on the console's network, for the host candidate; the
    /// <c>waker</c> ends the network thread's wait for a datagram (a <see cref="LoopbackDoorbell"/> on the transport's
    /// port, or a <see cref="NoWaker"/> for a transport that cannot wait); the <c>decoder</c> is the head's (it stays the
    /// head's to dispose, after the connection); the running title picks one of the player's <c>profiles</c>.
    /// </summary>
    public StreamConnection(IXHomeSessionService sessions, IDatagramTransport transport, IStreamWaker waker, uint hostAddress, IVideoDecoder decoder, IStreamAudioSink audio, IReadOnlyList<StreamProfile> profiles, TimeProvider clock, IRandomSource random, ILogger<StreamConnection> log)
    {
        _sessions = sessions;
        _transport = transport;
        _waker = waker;

        // The session's own pieces: where it streams from, its decoder, its sound and the player's profiles.
        _hostAddress = hostAddress;
        _decoder = decoder;
        _audio = audio;
        _profiles = profiles;

        // The thread's clock, randomness and log, and the profile until the console names its title.
        _clock = clock;
        _random = random;
        _log = log;
        _meter = new StreamStatsMeter(clock);
        _profile = ProfileChoice.For(profiles, null);
    }

    /// <summary>The newest decoded picture, for the render thread.</summary>
    public PictureMailbox Pictures => _pictures;

    /// <summary>The latest snapshot; a new one replaces it on every state change and once a second.</summary>
    public StreamStats Stats => Volatile.Read(ref _stats);

    public StreamState State => Stats.State;

    /// <summary>The running game's title id, once the console named it.</summary>
    public string? TitleId => Stats.TitleId;

    /// <summary>The profile for the running title: the default one until the console names a title a profile lists.</summary>
    public StreamProfile Profile => Volatile.Read(ref _profile);

    /// <summary>Starts the session on <paramref name="serverId"/> on the network thread and returns at once.</summary>
    public void Start(string serverId, StreamSettings settings)
    {
        if (_thread is not null)
        {
            throw new InvalidOperationException("A connection streams once.");
        }

        Publish(Stats with { State = StreamState.Starting });
        _thread = new Thread(() => Run(serverId, settings)) { IsBackground = true, Name = "Relay stream network" };
        _thread.Start();
    }

    /// <summary>
    /// The pad's state now, from any thread: the network thread writes the newest state of each pad onto the input
    /// channel as soon as it sees it, so states sent faster than it turns are merged, never lost.
    /// </summary>
    public void SendPad(byte index, in XboxPadState pad)
    {
        _pads.Post(index, in pad);
        _waker.Wake();
    }

    /// <summary>Ends the stream and stops the session on the console; returns once the network thread ended.</summary>
    public void Stop()
    {
        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
        }

        _thread?.Join();
    }

    /// <summary>Waits up to <paramref name="timeout"/> for the stream to end by itself; true when it has.</summary>
    public bool Join(TimeSpan timeout)
    {
        var thread = _thread;

        return thread is null || thread.Join(timeout);
    }

    public void Dispose()
    {
        Stop();
        _stop.Dispose();
    }

    /// <summary>The network thread: the whole session, its errors turned into the failed state, the session always stopped.</summary>
    private void Run(string serverId, StreamSettings settings)
    {
        StreamSession? session = null;
        using var video = new DecodeWorker(_decoder, _pictures, _clock);
        video.Start();

        try
        {
            session = Wait(_sessions.StartAsync(serverId, settings, _stop.Token));
            Wait(_sessions.WaitUntilReadyAsync(session, null, _stop.Token));
            Fail(RunSession(session, video));
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Stopped while the service was answering: the session is stopped below.
        }
        catch (Exception error)
        {
            Fail(error.Message);
        }
        finally
        {
            StopSession(session);
        }
    }

    /// <summary>Negotiates, connects and streams until stopped; returns why it failed, or null.</summary>
    private string? RunSession(StreamSession session, DecodeWorker video)
    {
        var local = IceCredentials.Create(_random);
        var certificate = DtlsCertificate.Create(new SecureRandom(), _clock.GetUtcNow());
        Publish(Stats with { State = StreamState.Negotiating });
        string answer = Wait(_sessions.ExchangeSdpAsync(session, StreamOffer.Create(local, certificate.Fingerprint, _random), _stop.Token));

        if (SessionIce.ReadCredentials(answer) is null)
        {
            return "the console's answer carries no ICE credentials";
        }

        using var link = new ConsoleLink(_transport, local, video, _audio, _pads, _clock, _random);
        var exchanged = Wait(_sessions.ExchangeIceAsync(session, [link.HostCandidate(_hostAddress)], _stop.Token));
        link.StartIce(answer, exchanged);
        Publish(Stats with { State = StreamState.Connecting });

        string? failure = Connect(link, certificate);

        if (failure is not null || _stop.IsCancellationRequested)
        {
            return failure;
        }

        Publish(Stats with { State = StreamState.Streaming, IceRoundTrip = link.Ice.RoundTrip, DtlsTime = link.DtlsElapsed });

        return Receive(session, link, video);
    }

    /// <summary>ICE, then DTLS, each within its deadline; returns why it failed, or null (also when stopped).</summary>
    private string? Connect(ConsoleLink link, DtlsCertificate certificate)
    {
        PumpUntil(link, IceDeadline, () => link.Ice.State != IceAgentState.Checking);

        if (link.Ice.State != IceAgentState.Connected)
        {
            return Unless("ICE found no path to the console");
        }

        if (!link.StartDtls(certificate))
        {
            return "the console's answer carries no DTLS fingerprint";
        }

        string? failure = null;

        if (!PumpUntil(link, DtlsDeadline, () => link.TryFinishDtls(out failure)))
        {
            return Unless("the DTLS handshake timed out");
        }

        if (failure is not null)
        {
            return $"the DTLS handshake failed: {failure}";
        }

        return null;
    }

    /// <summary>The stream's steady state: pump, publish the stats once a second, keep the session alive, until stopped.</summary>
    private string? Receive(StreamSession session, ConsoleLink link, DecodeWorker video)
    {
        long statsAt = _clock.GetTimestamp();
        long keepAliveAt = statsAt;
        Task? keepAlive = null;

        while (!_stop.IsCancellationRequested)
        {
            link.Pump();
            string? problem = Problem(link, video);

            if (problem is not null)
            {
                return problem;
            }

            NoteTitle(link);
            long now = _clock.GetTimestamp();

            if (now >= statsAt)
            {
                Measure(link, video);
                statsAt = now + Ticks(StatsInterval);
            }

            if (now >= keepAliveAt)
            {
                keepAlive = KeepAlive(session, keepAlive);
                keepAliveAt = now + Ticks(KeepAliveInterval);
            }

            WaitForWork();
        }

        return null;
    }

    /// <summary>Until a datagram arrives or a pad changes, where the transport can wait; a yield where it cannot.</summary>
    private void WaitForWork()
    {
        if (_transport is IDatagramWaiter waiter)
        {
            waiter.WaitForDatagram(DatagramWait);
        }
        else
        {
            Thread.Yield();
        }
    }

    /// <summary>
    /// Pumps the link until <paramref name="done"/> or the deadline, yielding between turns; false at the deadline or
    /// when stopped.
    /// </summary>
    private bool PumpUntil(ConsoleLink link, TimeSpan deadline, Func<bool> done)
    {
        long start = _clock.GetTimestamp();

        while (!_stop.IsCancellationRequested)
        {
            if (done())
            {
                return true;
            }

            if (_clock.GetElapsedTime(start) >= deadline)
            {
                return false;
            }

            link.Pump();
            Thread.Yield();
        }

        return false;
    }

    private static string? Problem(ConsoleLink link, DecodeWorker video)
    {
        var channels = link.Channels;

        if (channels is not null && channels.HasEnded)
        {
            return "the console closed the stream";
        }

        string? decoder = video.Problem;

        if (decoder is not null)
        {
            return $"the video decoder stopped: {decoder}";
        }

        return null;
    }

    /// <summary>A newly named title picks its profile.</summary>
    private void NoteTitle(ConsoleLink link)
    {
        string? titleId = link.Channels?.TitleId;

        if (titleId is null || titleId == Stats.TitleId)
        {
            return;
        }

        var profile = ProfileChoice.For(_profiles, titleId);
        Volatile.Write(ref _profile, profile);
        Publish(Stats with { TitleId = titleId });
        _log.TitleChanged(titleId, profile.Name);
    }

    private void Measure(ConsoleLink link, DecodeWorker video)
    {
        var media = link.Media;
        long reports = link.Channels?.InputReportsSent ?? 0;
        var counters = new StreamCounters(
            media?.AccessUnits ?? 0,
            video.Pictures,
            video.Decoded,
            video.DecodeTotal,
            video.TakeWindowWorst(),
            media?.Lost ?? 0,
            media?.KeyframeRequests ?? 0,
            reports,
            video.Width,
            video.Height);

        Publish(_meter.Measure(Stats, in counters) with { IceRoundTrip = link.Ice.RoundTrip });
    }

    /// <summary>A keep-alive goes out unless the last is still on its way; a refused one is logged, not fatal.</summary>
    private Task KeepAlive(StreamSession session, Task? previous)
    {
        if (previous is not null && !previous.IsCompleted)
        {
            return previous;
        }

        if (previous is not null && previous.IsFaulted)
        {
            _log.KeepAliveRefused(previous.Exception?.GetBaseException().Message ?? "unknown");
        }

        return _sessions.KeepAliveAsync(session, _stop.Token);
    }

    /// <summary>Stops the session on the console, even when the stream failed or was stopped; then the state is final.</summary>
    private void StopSession(StreamSession? session)
    {
        bool failed = Stats.State == StreamState.Failed;

        if (session is not null)
        {
            StopOnConsole(session, failed);
        }

        if (!failed)
        {
            Publish(Stats with { State = StreamState.Stopped });
        }
    }

    private void StopOnConsole(StreamSession session, bool failed)
    {
        if (!failed)
        {
            Publish(Stats with { State = StreamState.Stopping });
        }

        try
        {
            Wait(_sessions.StopAsync(session, CancellationToken.None));
        }
        catch (Exception error)
        {
            _log.StopRefused(error.Message);
        }
    }

    /// <summary>The reason, unless the stream was stopped meanwhile (a stop is not a failure).</summary>
    private string? Unless(string reason)
    {
        if (_stop.IsCancellationRequested)
        {
            return null;
        }

        return reason;
    }

    private void Fail(string? reason)
    {
        if (reason is null)
        {
            return;
        }

        Publish(Stats with { State = StreamState.Failed, FailureReason = reason });
        _log.Failed(reason);
    }

    private void Publish(StreamStats next)
    {
        var previous = Volatile.Read(ref _stats);
        Volatile.Write(ref _stats, next);

        if (previous.State != next.State)
        {
            _log.StateChanged(next.State);
        }
    }

    private long Ticks(TimeSpan span)
    {
        return span.Ticks * _clock.TimestampFrequency / TimeSpan.TicksPerSecond;
    }

    /// <summary>The service's calls run on the network thread itself, which has nothing else to do until they answer.</summary>
    private static T Wait<T>(Task<T> task)
    {
        return task.GetAwaiter().GetResult();
    }

    private static void Wait(Task task)
    {
        task.GetAwaiter().GetResult();
    }
}
