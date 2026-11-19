// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Collections;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.WebRtc.Ice;
using Phantom.Relay.Xbox.Session;
using SessionCandidate = Phantom.Relay.Xbox.Session.IceCandidate;
using WebRtcCandidate = Phantom.Relay.WebRtc.Ice.IceCandidate;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// ICE against the real console: our host candidate goes through the service's ICE exchange, the console's
/// candidates come from its answer and that exchange, and the agent runs on a real UDP socket until it connects or
/// gives up. Once connected it listens a little longer to see whether the console starts DTLS.
/// </summary>
public sealed class LiveIce : ILiveIce
{
    private const int Dtls = 0;
    private const int Rtp = 1;
    private const int Other = 2;
    private const int CountKinds = 3;

    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(2);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ListenAfterConnect = TimeSpan.FromSeconds(2);

    private readonly IXHomeSessionService _sessions;
    private readonly IDatagramTransport _transport;
    private readonly uint _hostAddress;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly ByteRing _ring;

    public LiveIce(IXHomeSessionService sessions, IDatagramTransport transport, uint hostAddress, TimeProvider clock, IRandomSource random)
    {
        _sessions = sessions;
        _transport = transport;
        _hostAddress = hostAddress;
        _clock = clock;
        _random = random;
        _ring = new ByteRing(ByteRing.CapacityFor(DatagramFrame.FrameLength(transport.MaxDatagramLength)));
    }

    public async Task<IceOutcome> RunAsync(StreamSession session, IceCredentials local, string answer, CancellationToken cancellation)
    {
        var remote = SessionIce.ReadCredentials(answer);

        if (remote is null)
        {
            return new IceOutcome(IceAgentState.Failed, null, TimeSpan.Zero, 0, 0, 0, 0);
        }

        var agent = new IceAgent(_transport, _clock, _random, local);
        var candidates = await CandidatesAsync(session, agent, answer, cancellation);
        long start = _clock.GetTimestamp();
        agent.Start(remote, candidates);

        var counts = new int[CountKinds];
        await PumpUntilSettledAsync(agent, counts, cancellation);
        var elapsed = _clock.GetElapsedTime(start);

        if (agent.State == IceAgentState.Connected)
        {
            await PumpAsync(agent, counts, ListenAfterConnect, cancellation);
        }

        return new IceOutcome(agent.State, agent.Selected, elapsed, candidates.Count, counts[Dtls], counts[Rtp], counts[Other]);
    }

    /// <summary>Sends our host candidate and gathers the console's from its answer and the exchange, without duplicates.</summary>
    private async Task<List<WebRtcCandidate>> CandidatesAsync(StreamSession session, IceAgent agent, string answer, CancellationToken cancellation)
    {
        var host = agent.HostCandidate(_hostAddress);
        SessionCandidate[] local = [new SessionCandidate(host.ToLine(), "0", 0)];
        var exchanged = await _sessions.ExchangeIceAsync(session, local, cancellation);
        var candidates = new List<WebRtcCandidate>(SessionIce.ReadCandidates(answer));

        foreach (var line in exchanged)
        {
            if (WebRtcCandidate.TryParse(line.Candidate, out var candidate) && !candidates.Exists(c => c.Endpoint == candidate!.Endpoint))
            {
                candidates.Add(candidate!);
            }
        }

        return candidates;
    }

    private async Task PumpUntilSettledAsync(IceAgent agent, int[] counts, CancellationToken cancellation)
    {
        long start = _clock.GetTimestamp();

        while (agent.State == IceAgentState.Checking && _clock.GetElapsedTime(start) < Deadline)
        {
            PumpOnce(agent, counts);
            await Task.Delay(Step, _clock, cancellation);
        }
    }

    private async Task PumpAsync(IceAgent agent, int[] counts, TimeSpan length, CancellationToken cancellation)
    {
        long start = _clock.GetTimestamp();

        while (_clock.GetElapsedTime(start) < length)
        {
            PumpOnce(agent, counts);
            await Task.Delay(Step, _clock, cancellation);
        }
    }

    private void PumpOnce(IceAgent agent, int[] counts)
    {
        agent.Service();
        _transport.Receive(_ring);

        while (_ring.TryPeek(out var frame))
        {
            var payload = DatagramFrame.Payload(frame);

            if (!agent.TryHandle(DatagramFrame.Source(frame), payload))
            {
                Count(payload, counts);
            }

            _ring.Release();
        }
    }

    private static void Count(ReadOnlySpan<byte> payload, int[] counts)
    {
        if (DatagramClass.IsDtls(payload))
        {
            counts[Dtls]++;
        }
        else if (DatagramClass.IsRtp(payload))
        {
            counts[Rtp]++;
        }
        else
        {
            counts[Other]++;
        }
    }
}
