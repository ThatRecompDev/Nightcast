// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session.Payloads;

namespace Phantom.Relay.Xbox.Session;

/// <summary>Reads the console list, skipping entries without a server id.</summary>
public static class XboxConsoles
{
    private const string UnknownValue = "Unknown";
    private const string DefaultName = "Xbox";

    public static IReadOnlyList<XboxConsole> From(IReadOnlyList<ConsoleReply>? replies)
    {
        var consoles = new List<XboxConsole>();

        if (replies is null)
        {
            return consoles;
        }

        foreach (var reply in replies)
        {
            if (string.IsNullOrEmpty(reply.ServerId))
            {
                continue;
            }

            consoles.Add(new XboxConsole(
                reply.ServerId,
                reply.DeviceName ?? DefaultName,
                reply.ConsoleType ?? UnknownValue,
                reply.PowerState ?? UnknownValue,
                reply.IsStreamingEnabled ?? true));
        }

        return consoles;
    }
}
