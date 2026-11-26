// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Ice;

/// <summary>
/// The ICE part of a session description: the first a=ice-ufrag and a=ice-pwd (with BUNDLE every m-line carries the
/// same pair) and every UDP IPv4 a=candidate line, a Teredo line as the IPv4 endpoints it names.
/// </summary>
public static class SessionIce
{
    private const string UfragAttribute = "a=ice-ufrag:";
    private const string PasswordAttribute = "a=ice-pwd:";
    private const string CandidateAttribute = "a=candidate:";

    /// <summary>The credentials, or null when the description has none; candidates it cannot use are skipped.</summary>
    public static IceCredentials? ReadCredentials(string sdp)
    {
        string? ufrag = null;
        string? password = null;

        foreach (string line in Lines(sdp))
        {
            if (ufrag is null && line.StartsWith(UfragAttribute, StringComparison.Ordinal))
            {
                ufrag = line[UfragAttribute.Length..];
            }
            else if (password is null && line.StartsWith(PasswordAttribute, StringComparison.Ordinal))
            {
                password = line[PasswordAttribute.Length..];
            }
        }

        if (ufrag is null || password is null)
        {
            return null;
        }

        return new IceCredentials(ufrag, password);
    }

    public static IReadOnlyList<IceCandidate> ReadCandidates(string sdp)
    {
        var candidates = new List<IceCandidate>();

        foreach (string line in Lines(sdp))
        {
            if (line.StartsWith(CandidateAttribute, StringComparison.Ordinal))
            {
                IceCandidate.ReadInto(line, candidates);
            }
        }

        return candidates;
    }

    private static IEnumerable<string> Lines(string sdp)
    {
        foreach (string line in sdp.Split('\n'))
        {
            yield return line.TrimEnd('\r');
        }
    }
}
