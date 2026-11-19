// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Stream.Feedback;

/// <summary>
/// What the client tells the console's video sender about the bitrate it can take, each an independent switch, all off
/// by default (<see cref="Off"/>: the offer and the RTCP of before): a bandwidth line in the offer's video section
/// (b=AS in kbit/s, RFC 4566, and b=TIAS in bit/s, RFC 3890); REMB (draft-alvestrand-rmcat-remb) announcing a fixed
/// bitrate, with goog-remb offered; and transport-wide congestion control (draft-holmer-rmcat-transport-wide-cc-
/// extensions-01), with its header extension and transport-cc offered. Measured live on 2026-11-19 (GTA VI, 1440p60,
/// wired LAN, 30 s each): without feedback 10.8 Mbit/s; REMB 5000 → 3.7 (and 720p), REMB 80000 → 26.9; b=AS 80000 →
/// 10.7 (ignored); transport-cc → 27.1 with no loss; the service's ceiling is about 30 Mbit/s
/// (docs/architecture/xhome.md, "Bitrate feedback").
/// </summary>
public sealed record BitrateFeedback
{
    /// <summary>No feedback: the offer and RTCP of before, about 11 Mbit/s at 1440p.</summary>
    public static BitrateFeedback Off { get; } = new();

    /// <summary>
    /// Transport-wide congestion control alone: the console's own controller climbs to the service's ceiling (about
    /// 30 Mbit/s) with no loss, and backs off by itself when the path is busy, which a fixed REMB claim never does.
    /// </summary>
    public static BitrateFeedback Recommended { get; } = new() { TransportWideCc = true };

    /// <summary>The video section's bandwidth line in kbit/s; 0 leaves it out.</summary>
    public int BandwidthKbps { get; init; }

    /// <summary>The bitrate each REMB announces, in kbit/s; 0 sends no REMB and offers no goog-remb.</summary>
    public int RembKbps { get; init; }

    /// <summary>Offers the transport-wide sequence extension and transport-cc, and sends the transport feedback.</summary>
    public bool TransportWideCc { get; init; }

    /// <summary>The switches that are on, for a log line: "b=AS 80000 kbit/s, REMB 80000 kbit/s, transport-cc", or "off".</summary>
    public string Describe()
    {
        var parts = new List<string>();

        if (BandwidthKbps > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"b=AS {BandwidthKbps} kbit/s"));
        }

        if (RembKbps > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"REMB {RembKbps} kbit/s"));
        }

        if (TransportWideCc)
        {
            parts.Add("transport-cc");
        }

        if (parts.Count == 0)
        {
            return "off";
        }

        return string.Join(", ", parts);
    }
}
