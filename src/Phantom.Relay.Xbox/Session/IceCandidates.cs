// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session.Payloads;

namespace Phantom.Relay.Xbox.Session;

/// <summary>
/// Moves candidates between the bare form WebRTC stacks use and the service's form: the client's go out as they are
/// with an end marker after them; the console's come back as SDP attributes ("a=candidate:...") with an end marker
/// that is dropped.
/// </summary>
public static class IceCandidates
{
    public static IReadOnlyList<IceCandidatePayload> ToPayloads(IReadOnlyList<IceCandidate> local)
    {
        var payloads = new List<IceCandidatePayload>(local.Count + 1);

        foreach (var candidate in local)
        {
            payloads.Add(new IceCandidatePayload(candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex));
        }

        payloads.Add(new IceCandidatePayload(XHomeProtocol.AttributePrefix + XHomeProtocol.EndOfCandidates, "0", 0));

        return payloads;
    }

    public static IReadOnlyList<IceCandidate> FromPayloads(IReadOnlyList<IceCandidatePayload>? remote)
    {
        var candidates = new List<IceCandidate>();

        if (remote is null)
        {
            return candidates;
        }

        foreach (var payload in remote)
        {
            string? line = payload.Candidate;

            if (string.IsNullOrEmpty(line) || line.Contains(XHomeProtocol.EndOfCandidates, StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith(XHomeProtocol.AttributePrefix, StringComparison.Ordinal))
            {
                line = line[XHomeProtocol.AttributePrefix.Length..];
            }

            candidates.Add(new IceCandidate(line, payload.SdpMid, payload.SdpMLineIndex));
        }

        return candidates;
    }
}
