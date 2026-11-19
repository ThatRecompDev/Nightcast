// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Tools.Probe;

/// <summary>The RTP payload types the answer gives H.264 (a=rtpmap:&lt;pt&gt; H264/90000).</summary>
public static class VideoPayloads
{
    private const string RtpMap = "a=rtpmap:";
    private const string H264 = "H264/";

    public static IReadOnlySet<int> Of(string sdp)
    {
        var payloads = new HashSet<int>();

        foreach (string raw in sdp.Split('\n'))
        {
            string line = raw.TrimEnd('\r');

            if (!line.StartsWith(RtpMap, StringComparison.Ordinal) || !line.Contains(H264, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string number = line[RtpMap.Length..].Split(' ')[0];

            if (int.TryParse(number, CultureInfo.InvariantCulture, out int payload))
            {
                payloads.Add(payload);
            }
        }

        return payloads;
    }
}
