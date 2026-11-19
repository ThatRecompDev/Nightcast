// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using Phantom.Relay.WebRtc.Rtp;

namespace Phantom.Relay.Stream.Sdp;

/// <summary>
/// What the console's answer kept of the bitrate feedback the offer proposed: the local id of the transport-wide
/// sequence extension (a=extmap, RFC 8285; 0 when the answer does not echo it, and then no packet carries it), whether
/// any payload keeps goog-remb or transport-cc (a=rtcp-fb, RFC 4585), and the first bandwidth line (b=), if any.
/// </summary>
public sealed record AnswerFeedback(int TransportWideId, bool Remb, bool TransportCc, string? BandwidthLine)
{
    private const string ExtMap = "a=extmap:";
    private const string RtcpFeedback = "a=rtcp-fb:";
    private const string Bandwidth = "b=";
    private const string RembName = " goog-remb";
    private const string TransportCcName = " transport-cc";

    /// <summary>The extmap's id is followed by an optional direction after a slash (RFC 8285 section 8).</summary>
    private const char DirectionSeparator = '/';

    /// <summary>An extmap line names the id, then the URI.</summary>
    private const int ExtMapFields = 2;

    public static AnswerFeedback Of(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var found = new AnswerFeedback(0, false, false, null);

        foreach (string raw in answer.Split('\n'))
        {
            found = found.With(raw.TrimEnd('\r'));
        }

        return found;
    }

    /// <summary>What the answer kept, for a log line: "extmap id 3, goog-remb, transport-cc, b=AS:80000", or "nothing".</summary>
    public string Describe()
    {
        var parts = new List<string>();

        if (TransportWideId > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"transport-wide extmap id {TransportWideId}"));
        }

        if (Remb)
        {
            parts.Add("goog-remb");
        }

        if (TransportCc)
        {
            parts.Add("transport-cc");
        }

        if (BandwidthLine is not null)
        {
            parts.Add(BandwidthLine);
        }

        if (parts.Count == 0)
        {
            return "nothing";
        }

        return string.Join(", ", parts);
    }

    private AnswerFeedback With(string line)
    {
        if (line.StartsWith(ExtMap, StringComparison.Ordinal) && TransportWideId == 0)
        {
            return this with { TransportWideId = TransportWideIdOf(line) };
        }

        if (line.StartsWith(RtcpFeedback, StringComparison.Ordinal))
        {
            return this with { Remb = Remb || line.EndsWith(RembName, StringComparison.Ordinal), TransportCc = TransportCc || line.EndsWith(TransportCcName, StringComparison.Ordinal) };
        }

        if (line.StartsWith(Bandwidth, StringComparison.Ordinal) && BandwidthLine is null)
        {
            return this with { BandwidthLine = line };
        }

        return this;
    }

    /// <summary>The id of an "a=extmap:&lt;id&gt;[/direction] &lt;uri&gt;" line naming the transport-wide extension; else 0.</summary>
    private static int TransportWideIdOf(string line)
    {
        string[] fields = line[ExtMap.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (fields.Length < ExtMapFields || fields[1] != TransportFeedbackFormat.ExtensionUri)
        {
            return 0;
        }

        string id = fields[0].Split(DirectionSeparator)[0];

        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            return 0;
        }

        return value;
    }
}
