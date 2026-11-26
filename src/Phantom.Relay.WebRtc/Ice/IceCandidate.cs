// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Engine.Net.Transport;

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// One UDP IPv4 candidate, read from or written as its SDP line (RFC 8839 section 5.1):
/// <c>candidate:&lt;foundation&gt; &lt;component&gt; udp &lt;priority&gt; &lt;address&gt; &lt;port&gt; typ &lt;kind&gt; ...</c>.
/// Other transports and IPv6 are skipped, since the engine's datagram transport is UDP over IPv4; a Teredo IPv6 line
/// is read as the IPv4 endpoints it names (<see cref="ReadInto"/>).
/// </summary>
public sealed record IceCandidate(string Foundation, int Component, uint Priority, DatagramEndpoint Endpoint, IceCandidateKind Kind)
{
    public const string Prefix = "candidate:";

    private const int FoundationField = 0;
    private const int ComponentField = 1;
    private const int TransportField = 2;
    private const int PriorityField = 3;
    private const int AddressField = 4;
    private const int PortField = 5;
    private const int TypeKeywordField = 6;
    private const int TypeField = 7;
    private const int FieldCount = 8;
    private const int Octets = 4;
    private const int BitsPerOctet = 8;
    private const string AttributePrefix = "a=";
    private const string Udp = "udp";
    private const string TypeKeyword = "typ";

    /// <summary>Reads a candidate line, with or without its "a=" prefix; false for anything not UDP over IPv4.</summary>
    public static bool TryParse(string line, out IceCandidate? candidate)
    {
        candidate = null;

        return TryReadFields(line, out string[] fields) && TryBuild(fields, out candidate);
    }

    /// <summary>
    /// Adds what a candidate line gives to <paramref name="into"/>: its UDP IPv4 candidate, or, for a UDP line at a
    /// Teredo IPv6 address (<see cref="TeredoAddress"/>), the router's public IPv4 address at the line's own port (open
    /// where the router forwards it, as UPnP does) and at the Teredo tunnel's port, both server reflexive. Anything else
    /// adds nothing. Returns how many were added.
    /// </summary>
    public static int ReadInto(string line, ICollection<IceCandidate> into)
    {
        if (TryParse(line, out var candidate))
        {
            into.Add(candidate!);

            return 1;
        }

        if (!TryReadFields(line, out string[] fields)
            || !TeredoAddress.TryRead(fields[AddressField], out uint address, out ushort tunnelPort)
            || !int.TryParse(fields[ComponentField], CultureInfo.InvariantCulture, out int component)
            || !ushort.TryParse(fields[PortField], CultureInfo.InvariantCulture, out ushort port))
        {
            return 0;
        }

        int before = into.Count;
        uint direct = IcePriorities.Candidate(IcePriorities.ServerReflexivePreference, IcePriorities.SingleLocalPreference, component);
        into.Add(new IceCandidate(fields[FoundationField], component, direct, new DatagramEndpoint(address, port), IceCandidateKind.ServerReflexive));

        if (tunnelPort != port)
        {
            uint tunnel = IcePriorities.Candidate(IcePriorities.ServerReflexivePreference, IcePriorities.SingleLocalPreference - 1, component);
            into.Add(new IceCandidate(fields[FoundationField], component, tunnel, new DatagramEndpoint(address, tunnelPort), IceCandidateKind.ServerReflexive));
        }

        return into.Count - before;
    }

    /// <summary>The candidate's SDP line without "a=", for the signaling channel.</summary>
    public string ToLine()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}{Foundation} {Component} {Udp} {Priority} {Address(Endpoint)} {Endpoint.Port} {TypeKeyword} {KindName(Kind)}");
    }

    /// <summary>The fields of a UDP candidate line, with or without its "a=" prefix, at any address.</summary>
    private static bool TryReadFields(string line, out string[] fields)
    {
        fields = [];
        var text = line.AsSpan().Trim();

        if (text.StartsWith(AttributePrefix, StringComparison.Ordinal))
        {
            text = text[AttributePrefix.Length..];
        }

        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        fields = text[Prefix.Length..].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return fields.Length >= FieldCount && fields[TransportField].Equals(Udp, StringComparison.OrdinalIgnoreCase) && fields[TypeKeywordField] == TypeKeyword;
    }

    private static bool TryBuild(string[] fields, out IceCandidate? candidate)
    {
        candidate = null;

        if (!int.TryParse(fields[ComponentField], CultureInfo.InvariantCulture, out int component)
            || !uint.TryParse(fields[PriorityField], CultureInfo.InvariantCulture, out uint priority)
            || !ushort.TryParse(fields[PortField], CultureInfo.InvariantCulture, out ushort port)
            || !TryParseAddress(fields[AddressField], out uint address)
            || !TryParseKind(fields[TypeField], out var kind))
        {
            return false;
        }

        candidate = new IceCandidate(fields[FoundationField], component, priority, new DatagramEndpoint(address, port), kind);

        return true;
    }

    private static bool TryParseAddress(string text, out uint address)
    {
        address = 0;
        string[] parts = text.Split('.');

        if (parts.Length != Octets)
        {
            return false;
        }

        foreach (string part in parts)
        {
            if (!byte.TryParse(part, CultureInfo.InvariantCulture, out byte octet))
            {
                return false;
            }

            address = (address << BitsPerOctet) | octet;
        }

        return true;
    }

    private static bool TryParseKind(string text, out IceCandidateKind kind)
    {
        switch (text)
        {
            case "host":
                kind = IceCandidateKind.Host;

                return true;
            case "srflx":
                kind = IceCandidateKind.ServerReflexive;

                return true;
            case "prflx":
                kind = IceCandidateKind.PeerReflexive;

                return true;
            case "relay":
                kind = IceCandidateKind.Relayed;

                return true;
            default:
                kind = IceCandidateKind.Host;

                return false;
        }
    }

    private static string KindName(IceCandidateKind kind)
    {
        switch (kind)
        {
            case IceCandidateKind.ServerReflexive:
                return "srflx";
            case IceCandidateKind.PeerReflexive:
                return "prflx";
            case IceCandidateKind.Relayed:
                return "relay";
            default:
                return "host";
        }
    }

    private static string Address(DatagramEndpoint endpoint)
    {
        var octets = new byte[Octets];
        endpoint.WriteOctets(octets);

        return string.Join('.', octets);
    }
}
