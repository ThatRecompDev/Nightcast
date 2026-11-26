// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Text.Json;

namespace Phantom.Relay.Desktop.Relay;

/// <summary>
/// Picks the TURN URL the stream can use from an RTCIceServer list: plain <c>turn:</c> over UDP (no transport
/// parameter, or <c>transport=udp</c>), since the stream's transport is UDP; TCP and TLS URLs are passed over.
/// </summary>
internal static class TurnUrls
{
    public const int DefaultPort = 3478;

    private const string Scheme = "turn:";
    private const string UdpTransport = "transport=udp";
    private const char QueryStart = '?';
    private const char PortSeparator = ':';

    public static bool TryChoose(IReadOnlyList<IceServerEntry> servers, out string host, out ushort port, out string username, out string credential)
    {
        host = string.Empty;
        port = 0;
        username = string.Empty;
        credential = string.Empty;

        foreach (var server in servers)
        {
            if (server.Username is null || server.Credential is null)
            {
                continue;
            }

            foreach (string url in Urls(server.Urls))
            {
                if (TryRead(url, out host, out port))
                {
                    username = server.Username;
                    credential = server.Credential;

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The host and port of a <c>turn:host[:port][?transport=udp]</c> URL; false for any other.</summary>
    public static bool TryRead(string url, out string host, out ushort port)
    {
        host = string.Empty;
        port = 0;

        if (!url.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = url[Scheme.Length..];
        int query = rest.IndexOf(QueryStart, StringComparison.Ordinal);

        if (query >= 0)
        {
            if (!rest[(query + 1)..].Equals(UdpTransport, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            rest = rest[..query];
        }

        int separator = rest.LastIndexOf(PortSeparator);
        port = DefaultPort;

        if (separator >= 0)
        {
            if (!ushort.TryParse(rest[(separator + 1)..], CultureInfo.InvariantCulture, out port))
            {
                return false;
            }

            rest = rest[..separator];
        }

        host = rest;

        return host.Length > 0;
    }

    private static IEnumerable<string> Urls(JsonElement urls)
    {
        if (urls.ValueKind == JsonValueKind.String)
        {
            yield return urls.GetString()!;
        }
        else if (urls.ValueKind == JsonValueKind.Array)
        {
            foreach (var url in urls.EnumerateArray())
            {
                if (url.ValueKind == JsonValueKind.String)
                {
                    yield return url.GetString()!;
                }
            }
        }
    }
}
