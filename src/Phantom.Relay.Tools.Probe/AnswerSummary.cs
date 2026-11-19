// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The lines of the console's SDP answer that say what it will send: each media line and its codec, format, bandwidth
/// and picture-size attributes. Credentials, fingerprints and candidates are left out.
/// </summary>
public static class AnswerSummary
{
    private static readonly string[] Kept = ["m=", "b=", "a=rtpmap:", "a=fmtp:", "a=imageattr:", "a=framerate:", "a=sendonly", "a=recvonly", "a=sendrecv", "a=inactive", "a=setup:"];

    public static IReadOnlyList<string> Of(string answer)
    {
        var lines = new List<string>();

        foreach (string line in answer.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');

            if (IsKept(trimmed))
            {
                lines.Add(trimmed);
            }
        }

        return lines;
    }

    private static bool IsKept(string line)
    {
        foreach (string prefix in Kept)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
