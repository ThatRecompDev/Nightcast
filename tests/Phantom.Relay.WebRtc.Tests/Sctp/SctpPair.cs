// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.WebRtc.Sctp;

namespace Phantom.Relay.WebRtc.Tests.Sctp;

/// <summary>
/// Two associations joined by two fake links on one manual clock: the client connects, as this side does with the
/// console, and the server answers. Each step services both, delivers what each link holds and moves the clock.
/// </summary>
public sealed class SctpPair
{
    public const int StepMilliseconds = 5;

    public SctpPair(int seed)
        : this(SctpOptions.Default, seed)
    {
    }

    private SctpPair(SctpOptions options, int seed)
    {
        ToServer = new FakeSctpLink(seed);
        ToClient = new FakeSctpLink(seed + 1);
        Client = new SctpAssociation(ToServer, Clock, new SeededRandomSource((ulong)seed + 2), options);
        Server = new SctpAssociation(ToClient, Clock, new SeededRandomSource((ulong)seed + 3), options);
    }

    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));

    public FakeSctpLink ToServer { get; }

    public FakeSctpLink ToClient { get; }

    public SctpAssociation Client { get; }

    public SctpAssociation Server { get; }

    /// <summary>A pair with smaller buffers, for tests that need them to fill.</summary>
    public static SctpPair WithOptions(SctpOptions options, int seed)
    {
        return new SctpPair(options, seed);
    }

    /// <summary>Connects the client and runs until both sides are established.</summary>
    public void Connect()
    {
        Client.Connect();
        Run(20);
    }

    public void Run(int steps)
    {
        Run(steps, StepMilliseconds);
    }

    public void Run(int steps, int stepMilliseconds)
    {
        for (int step = 0; step < steps; step++)
        {
            Step(stepMilliseconds);
        }
    }

    public void Step(int stepMilliseconds)
    {
        Client.Service();
        Server.Service();
        ToServer.DeliverTo(Server);
        ToClient.DeliverTo(Client);
        Clock.AdvanceMilliseconds(stepMilliseconds);
    }

    /// <summary>Reads every whole message waiting at <paramref name="association"/>.</summary>
    public static List<(ushort Stream, uint Protocol, byte[] Payload)> Drain(SctpAssociation association)
    {
        var messages = new List<(ushort Stream, uint Protocol, byte[] Payload)>();

        while (association.TryPeek(out ushort stream, out uint protocol, out var payload))
        {
            messages.Add((stream, protocol, payload.ToArray()));
            association.Release();
        }

        return messages;
    }
}
