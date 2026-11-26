// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Net.Transport;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.WebRtc.Turn;

namespace Phantom.Relay.Desktop.Relay;

/// <summary>
/// The stream's TURN relay from the user's own Cloudflare TURN key, when they have one: <c>turn.json</c>
/// (<c>{"keyId": "…", "apiToken": "…"}</c>, the devices gateway's shape) beside the executable, or in Nightcast's
/// folder under the user's local application data. The key never leaves this machine; Cloudflare makes credentials
/// from it that expire on their own. Without the file the relay is off and nothing is asked. Public releases carry no
/// such file (docs/how-we-work.md, 1a).
/// </summary>
internal static class CloudflareTurn
{
    public const string KeyFileName = "turn.json";

    /// <summary>Cloudflare's credential call for a TURN key, around the key's id (developers.cloudflare.com, Realtime TURN).</summary>
    private const string CredentialsBase = "https://rtc.live.cloudflare.com/v1/turn/keys/";

    private const string CredentialsPath = "/credentials/generate-ice-servers";

    private const string UserAgent = RelayApp.Name + "/1.0";

    /// <summary>The allocation refreshes with these credentials, so they outlast the longest session: a day.</summary>
    private static readonly int LifetimeSeconds = (int)TimeSpan.FromDays(1).TotalSeconds;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>The relay to give the stream, or null (no key file, or Cloudflare or DNS did not answer); never throws.</summary>
    public static TurnServer? Find(ILogger log)
    {
        string? path = KeyFile();

        if (path is null)
        {
            return null;
        }

        try
        {
            using var cancellation = new CancellationTokenSource(Timeout);
            var server = FindAsync(path, cancellation.Token).GetAwaiter().GetResult();

            if (server is null)
            {
                log.RelayOff("the key file or Cloudflare's answer has no usable TURN server");
            }
            else if (log.IsEnabled(LogLevel.Information))
            {
                log.RelayReady(server.ToString());
            }

            return server;
        }
        catch (Exception problem) when (problem is HttpRequestException or JsonException or IOException or SocketException or OperationCanceledException)
        {
            log.RelayOff(problem.GetType().Name);

            return null;
        }
    }

    /// <summary>The key file beside the executable first (a private build carries it there), else in the app's own folder.</summary>
    private static string? KeyFile()
    {
        string beside = Path.Combine(AppContext.BaseDirectory, KeyFileName);

        if (File.Exists(beside))
        {
            return beside;
        }

        string kept = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), RelayApp.Name, KeyFileName);

        if (File.Exists(kept))
        {
            return kept;
        }

        return null;
    }

    private static async Task<TurnServer?> FindAsync(string path, CancellationToken cancellation)
    {
        var key = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(path, cancellation), TurnJsonContext.Default.TurnKey);

        if (key is null || string.IsNullOrEmpty(key.KeyId) || string.IsNullOrEmpty(key.ApiToken))
        {
            return null;
        }

        // Cloudflare refuses a request without a user agent (seen by the devices gateway); a generic one, nothing of the user's.
        using var client = new HttpClient { Timeout = Timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        using var request = new HttpRequestMessage(HttpMethod.Post, CredentialsBase + Uri.EscapeDataString(key.KeyId) + CredentialsPath)
        {
            Content = JsonContent.Create(new TurnCredentialRequest(LifetimeSeconds), TurnJsonContext.Default.TurnCredentialRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.ApiToken);

        using var response = await client.SendAsync(request, cancellation);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var reply = await response.Content.ReadFromJsonAsync(TurnJsonContext.Default.IceServersReply, cancellation);

        if (reply?.IceServers is null || !TurnUrls.TryChoose(reply.IceServers, out string host, out ushort port, out string username, out string credential))
        {
            return null;
        }

        foreach (var address in await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cancellation))
        {
            return new TurnServer(new DatagramEndpoint(ToUInt(address), port), username, credential);
        }

        return null;
    }

    private static uint ToUInt(IPAddress address)
    {
        Span<byte> octets = stackalloc byte[sizeof(uint)];
        address.TryWriteBytes(octets, out _);

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(octets);
    }
}
