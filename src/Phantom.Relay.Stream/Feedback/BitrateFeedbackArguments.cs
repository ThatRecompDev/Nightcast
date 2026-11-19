// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Stream.Feedback;

/// <summary>
/// The bitrate feedback switches on a command line, the same for the probe and the desktop, so a combination found
/// with the probe turns on in the app without a rebuild: <c>--bandwidth-line &lt;kbit/s&gt;</c>, <c>--remb
/// &lt;kbit/s&gt;</c> and <c>--twcc</c>, in any order and combination. Other arguments are left to the caller.
/// </summary>
public static class BitrateFeedbackArguments
{
    public const string BandwidthLine = "--bandwidth-line";
    public const string Remb = "--remb";
    public const string TransportWideCc = "--twcc";

    /// <summary>The highest bitrate a switch takes: 1 Gbit/s, far past what a LAN stream needs.</summary>
    public const int MaxKbps = 1_000_000;

    /// <summary>The switches' help, one line.</summary>
    public const string Usage = "--bandwidth-line <kbit/s> (b=AS/TIAS in the offer), --remb <kbit/s> (REMB feedback), --twcc (transport-wide feedback)";

    /// <summary>The value a switch takes, after it.</summary>
    private const int ValueOffset = 1;

    /// <summary>
    /// The feedback the switches in <paramref name="args"/> ask for (<see cref="BitrateFeedback.Off"/> without any);
    /// false with the <paramref name="error"/> when a bitrate is missing or not 1 to <see cref="MaxKbps"/>.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> args, out BitrateFeedback feedback, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        feedback = BitrateFeedback.Off;
        error = null;
        int index = 0;

        while (index < args.Count)
        {
            string name = args[index];

            if (name == TransportWideCc)
            {
                feedback = feedback with { TransportWideCc = true };
            }
            else if (name == BandwidthLine || name == Remb)
            {
                if (!TryKbps(args, index + ValueOffset, out int kbps))
                {
                    error = $"{name} takes a bitrate in kbit/s from 1 to {MaxKbps}, such as {name} 80000";

                    return false;
                }

                feedback = With(feedback, name, kbps);
                index += ValueOffset;
            }

            index++;
        }

        return true;
    }

    private static BitrateFeedback With(BitrateFeedback feedback, string name, int kbps)
    {
        if (name == BandwidthLine)
        {
            return feedback with { BandwidthKbps = kbps };
        }

        return feedback with { RembKbps = kbps };
    }

    private static bool TryKbps(IReadOnlyList<string> args, int index, out int kbps)
    {
        kbps = 0;

        return index < args.Count
            && int.TryParse(args[index], NumberStyles.None, CultureInfo.InvariantCulture, out kbps)
            && kbps is > 0 and <= MaxKbps;
    }
}
